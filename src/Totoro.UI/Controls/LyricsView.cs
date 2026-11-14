using System.Numerics;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NMeCab.Specialized;
using Totoro.Core.Services;

namespace Totoro.UI.Controls;

/// <summary>
/// A song's lyrics as it plays, followed every frame while <see cref="IsActive"/>: the line being sung in the middle,
/// its words lighting up as they're sung (the whole line at once, if only lines are timed), with the line before above
/// it and the line after below, both dimmer. As a line ends they all slide up a place. It's always three lines tall,
/// whatever the lines are. Japanese words can show their reading above them (furigana, over kanji) and in Latin letters
/// below (romaji).
/// </summary>
public sealed partial class LyricsView : Grid
{
	// How dim the words not sung yet are, and the lines before and after the one sung.
	private const double Unsung = 0.6;
	private const double Aside = 0.4;
	// The place of the line sung: the middle one of three (0 to 2); a line comes in from 3 and leaves to -1.
	private const int Middle = 1;
	private const double Gap = 6;
	// How long a line takes to slide up a place and brighten or dim: slow enough to follow.
	private static readonly TimeSpan s_move = TimeSpan.FromMilliseconds(800);

	// The lines shown, each in its place; and those that have left, until they've slid out.
	private readonly Dictionary<LyricLine, Shown> _shown = new(ReferenceEqualityComparer.Instance);
	private readonly List<(FrameworkElement Element, DateTime Gone)> _leaving = [];
	private bool _isRendering;

	public LyricsView()
	{
		// Lines slide in and out past its edges: they show within it, not over what's around the lyrics.
		SizeChanged += (_, e) => Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
		Loaded += (_, _) => UpdateRendering();
		Unloaded += (_, _) => StopRendering();
		Reset();
	}

	public static readonly DependencyProperty LinesProperty =
		DependencyProperty.Register(nameof(Lines), typeof(IReadOnlyList<LyricLine>), typeof(LyricsView),
									new PropertyMetadata(null, (d, _) => ((LyricsView)d).Reset()));

	public static readonly DependencyProperty SourceProperty =
		DependencyProperty.Register(nameof(Source), typeof(ISongPlayer), typeof(LyricsView), new PropertyMetadata(null));

	public static readonly DependencyProperty ReadingsProperty =
		DependencyProperty.Register(nameof(Readings), typeof(LyricReadings), typeof(LyricsView),
									new PropertyMetadata(LyricReadings.None, (d, _) => ((LyricsView)d).Reset()));

	public static readonly DependencyProperty IsActiveProperty =
		DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(LyricsView),
									new PropertyMetadata(false, (d, _) => ((LyricsView)d).UpdateRendering()));

	public IReadOnlyList<LyricLine>? Lines
	{
		get => (IReadOnlyList<LyricLine>?)GetValue(LinesProperty);
		set => SetValue(LinesProperty, value);
	}

	/// <summary>What's playing, whose position the lyrics follow.</summary>
	public ISongPlayer? Source
	{
		get => (ISongPlayer?)GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

	public LyricReadings Readings
	{
		get => (LyricReadings)GetValue(ReadingsProperty);
		set => SetValue(ReadingsProperty, value);
	}

	/// <summary>Whether to follow the song (while it's the one playing or paused).</summary>
	public bool IsActive
	{
		get => (bool)GetValue(IsActiveProperty);
		set => SetValue(IsActiveProperty, value);
	}

	private bool ShowsFurigana => Readings is not (LyricReadings.Romaji or LyricReadings.None);

	private bool ShowsRomaji => Readings is not (LyricReadings.Furigana or LyricReadings.None);

	/// <summary>A line's height: its text's, and its readings' rows'.</summary>
	private double LineHeight => 26 + (ShowsFurigana ? 18 : 0) + (ShowsRomaji ? 18 : 0);

	/// <summary>
	/// The line as what's shown of it, in order: each Japanese word with its reading in hiragana, anything else word by
	/// word with none.
	/// </summary>
	internal static IEnumerable<(string Text, string? Reading)> Split(string line)
	{
		foreach (Match run in JapaneseOrNot().Matches(line))
		{
			if (!Lyrics.IsJapanese(run.Value[0]))
			{
				foreach (var word in run.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
				{
					yield return (word, null);
				}

				continue;
			}

			if (s_tagger.Value is not { } tagger)
			{
				// Its dictionary is missing: the text, without readings.
				yield return (run.Value, null);
				continue;
			}

			foreach (var word in tagger.Parse(run.Value))
			{
				// A word the dictionary doesn't know has no reading, unless it's kana: then it reads as written.
				var reading = !string.IsNullOrEmpty(word.Reading) ? word.Reading
					: word.Surface.Any(Lyrics.IsKanji) ? null
					: word.Surface;
				yield return (word.Surface, reading is null ? null : Romaji.ToHiragana(reading));
			}
		}
	}

	// Windows' own (JapanesePhoneticAnalyzer) returns nothing unless Japanese is installed; this carries its dictionary.
	private static readonly Lazy<MeCabIpaDicTagger?> s_tagger = new(() =>
	{
		try
		{
			// Beside the app, or in this library's folder of it (where a project that references this one gets it).
			var dictionary = new[] { "IpaDic", Path.Combine("Totoro.UI", "IpaDic") }
				.Select(x => Path.Combine(AppContext.BaseDirectory, x))
				.FirstOrDefault(Directory.Exists);
			return dictionary is null ? null : MeCabIpaDicTagger.Create(dictionary);
		}
		catch (Exception)
		{
			return null;
		}
	});

	/// <summary>
	/// When the part of <paramref name="line"/> after <paramref name="offset"/> of its characters (spaces not counted)
	/// starts being sung: its word's time, or the line's if its words aren't timed.
	/// </summary>
	internal static TimeSpan StartOf(LyricLine line, int offset)
	{
		var start = line.Start;
		var end = 0;
		foreach (var word in line.Words)
		{
			start = word.Start;
			end += word.Text.Count(c => !char.IsWhiteSpace(c));
			if (end > offset)
			{
				break;
			}
		}

		return start;
	}

	/// <summary>
	/// The place (0 to 2, top to bottom) of each line to show when the line at <paramref name="index"/> is the one being
	/// sung (-1 before the first): it in the middle, the one before above, the one after below.
	/// </summary>
	internal static IEnumerable<(LyricLine Line, int Place)> Places(IReadOnlyList<LyricLine> lines, int index)
	{
		for (var i = Math.Max(0, index - 1); i <= index + 1 && i < lines.Count; i++)
		{
			yield return (lines[i], i - index + Middle);
		}
	}

	[GeneratedRegex(@"[぀-ヿ㐀-䶿一-鿿々]+|[^぀-ヿ㐀-䶿一-鿿々]+")]
	private static partial Regex JapaneseOrNot();

	private void UpdateRendering()
	{
		if (IsActive && IsLoaded)
		{
			if (!_isRendering)
			{
				_isRendering = true;
				CompositionTarget.Rendering += OnRendering;
			}
		}
		else
		{
			StopRendering();
			Reset();
		}
	}

	private void StopRendering()
	{
		if (_isRendering)
		{
			_isRendering = false;
			CompositionTarget.Rendering -= OnRendering;
		}
	}

	/// <summary>Nothing shown, and three lines tall, for other lyrics or other readings.</summary>
	private void Reset()
	{
		_shown.Clear();
		_leaving.Clear();
		Children.Clear();
		Height = (LineHeight * 3) + (Gap * 2);
	}

	private void OnRendering(object? sender, object e)
	{
		var position = Source?.Position ?? TimeSpan.Zero;
		var lines = Lines ?? [];
		var index = -1;
		while (index + 1 < lines.Count && lines[index + 1].Start <= position)
		{
			index++;
		}

		// Before the first line it's already in the middle (not lit yet), with the second under it: not two empty places.
		var places = Places(lines, Math.Max(index, 0)).ToList();
		// Nothing shown yet (or it was reset): the lines are put in their places, not slid into them.
		var slides = _shown.Count > 0;
		foreach (var line in _shown.Keys.Where(x => !places.Any(y => ReferenceEquals(x, y.Line))).ToList())
		{
			// Up and out, fading.
			var gone = _shown[line];
			_shown.Remove(line);
			Move(gone, gone.Place - 1, 0, slides: true);
			_leaving.Add((gone.Element, DateTime.UtcNow + s_move));
		}

		foreach (var (line, place) in places)
		{
			if (!_shown.TryGetValue(line, out var shown))
			{
				// From the place under its own.
				shown = _shown[line] = Build(line, place + 1);
				Children.Add(shown.Element);
			}

			if (shown.Place != place || !slides)
			{
				Move(shown, place, place == Middle ? 1 : Aside, slides);
			}

			foreach (var (start, cell) in shown.Cells)
			{
				cell.Opacity = place != Middle || start <= position ? 1 : Unsung;
			}
		}

		for (var i = _leaving.Count - 1; i >= 0; i--)
		{
			if (_leaving[i].Gone <= DateTime.UtcNow)
			{
				Children.Remove(_leaving[i].Element);
				_leaving.RemoveAt(i);
			}
		}
	}

	/// <summary>Moves the line to a place (sliding it there from where it is, if <paramref name="slides"/>) and sets how bright it is.</summary>
	private void Move(Shown shown, int place, double opacity, bool slides)
	{
		float Top(int at) => (float)(at * (LineHeight + Gap));
		var from = shown.Place;
		shown.Place = place;
		shown.Element.Opacity = opacity;
		shown.Element.Translation = new Vector3(0, Top(place), 0);
		if (!slides || from == place)
		{
			return;
		}

		try
		{
			// The line's own Translation, animated by the compositor. Not through ElementCompositionPreview: it refuses
			// (access denied) an element that has an OpacityTransition, as a line does.
			var compositor = CompositionTarget.GetCompositorForCurrentThread();
			var slide = compositor.CreateVector3KeyFrameAnimation();
			slide.Target = nameof(Translation);
			slide.InsertKeyFrame(0, new Vector3(0, Top(from), 0));
			slide.InsertKeyFrame(1, new Vector3(0, Top(place), 0), compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(0.2f, 1)));
			slide.Duration = s_move;
			shown.Element.StartAnimation(slide);
		}
		catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
		{
			// The line is in its place without the slide; thrown on, it would stop every frame's drawing (the spectrum's too).
		}
	}

	/// <summary>A line's words in a row (shrunk to fit, if it's too long), at <paramref name="place"/>, not showing yet.</summary>
	private Shown Build(LyricLine line, int place)
	{
		var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
		List<(TimeSpan Start, UIElement Cell)> cells = [];
		var words = Split(line.Text).ToList();
		// Their rows only on a line that has some: not on one in English.
		var furigana = ShowsFurigana && words.Any(x => x.Reading is not null && x.Text.Any(Lyrics.IsKanji));
		var romaji = ShowsRomaji && words.Any(x => x.Reading is not null);
		var offset = 0;
		foreach (var (text, reading) in words)
		{
			var cell = new StackPanel();
			if (furigana)
			{
				// On every word, so the words stay level; only kanji has one to show.
				cell.Children.Add(Small(reading is not null && text.Any(Lyrics.IsKanji) ? reading : ""));
			}

			cell.Children.Add(new TextBlock { Text = text, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center });
			if (romaji)
			{
				// The particles は and へ are said "wa" and "e".
				cell.Children.Add(Small(reading is null ? "" : text switch { "は" => "wa", "へ" => "e", _ => Romaji.FromKana(reading) }));
			}

			cells.Add((StartOf(line, offset), cell));
			row.Children.Add(cell);
			offset += text.Count(c => !char.IsWhiteSpace(c));
		}

		var element = new Viewbox
		{
			Child = row,
			Height = LineHeight,
			StretchDirection = StretchDirection.DownOnly,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			Opacity = 0,
			Translation = new Vector3(0, (float)(place * (LineHeight + Gap)), 0),
		};
		// After its first opacity, so that it fades in from nothing.
		element.OpacityTransition = new ScalarTransition { Duration = s_move };
		return new Shown(element, cells) { Place = place };
	}

	// A space when there's nothing to show, so it's as tall as the others.
	private static TextBlock Small(string text) => new()
	{
		Text = text.Length > 0 ? text : " ",
		FontSize = 13,
		HorizontalAlignment = HorizontalAlignment.Center,
	};

	/// <summary>A line as it's shown, with when each of its words starts being sung, and the place it's in.</summary>
	private sealed class Shown(FrameworkElement element, List<(TimeSpan Start, UIElement Cell)> cells)
	{
		public FrameworkElement Element { get; } = element;

		public List<(TimeSpan Start, UIElement Cell)> Cells { get; } = cells;

		public int Place { get; set; }
	}
}
