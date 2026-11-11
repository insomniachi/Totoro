using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using Totoro.Core.Modules.Media;

namespace Totoro.Views.Player;

/// <summary>
/// Draws the built-in player's seek bar track: the episode, and its opening, ending and recap in their own colours, with
/// a gap between each; how far playback is; and where the episode counts as watched.
/// </summary>
/// <param name="track">The track the sections span.</param>
/// <param name="trackSections">Where the dim sections go.</param>
/// <param name="playedSections">Where the bright ones go, clipped to how far playback is.</param>
/// <param name="watchedMark">Where the watched notch goes.</param>
internal sealed class SeekBarPainter(FrameworkElement track, Canvas trackSections, Canvas playedSections, Canvas watchedMark)
{
	// The seek bar's sections: the opening (and recap) and the ending, in Windows' yellow and teal; the episode in the accent colour.
	private static readonly Color s_openingColor = Color.FromArgb(255, 0xFF, 0xB9, 0x00);
	private static readonly Color s_endingColor = Color.FromArgb(255, 0x00, 0xB7, 0xC3);

	// The slider's thumb (SliderHorizontalThumbWidth), and the gap between the track's sections.
	private const double ThumbWidth = 18;
	private const double SectionGap = 3;

	/// <summary>
	/// Draws the track in sections. Drawn twice, dim and bright: the bright one is clipped to how far playback is. False
	/// when the track has no width yet, so nothing was drawn.
	/// </summary>
	public bool DrawSections(double duration, IReadOnlyList<MediaChapter> chapters)
	{
		trackSections.Children.Clear();
		playedSections.Children.Clear();
		var width = track.ActualWidth;
		if (width <= 0)
		{
			return false;
		}

		foreach (var (start, end, type) in GetSections(duration, chapters))
		{
			// The first and last reach the track's ends; the others meet with a gap.
			var left = start <= 0 ? 0 : ToTrackX(start, width) + SectionGap / 2;
			var right = end >= 1 ? width : ToTrackX(end, width) - SectionGap / 2;
			if (right - left < 1)
			{
				continue;
			}

			foreach (var (canvas, isPlayed) in new[] { (trackSections, false), (playedSections, true) })
			{
				var section = new Rectangle
				{
					Width = right - left,
					Height = track.ActualHeight,
					RadiusX = 2,
					RadiusY = 2,
					Fill = new SolidColorBrush(SectionColor(type, isPlayed)),
				};
				Canvas.SetLeft(section, left);
				canvas.Children.Add(section);
			}
		}

		return true;
	}

	/// <summary>
	/// A notch across the track where the episode counts as watched, while it hasn't been yet (then a check by its length
	/// says it has).
	/// </summary>
	public void DrawWatchedMark(bool isWatched, double watchedAt, double duration)
	{
		watchedMark.Children.Clear();
		var width = track.ActualWidth;
		if (isWatched || watchedAt < 0 || duration <= 0 || width <= 0)
		{
			return;
		}

		const double NotchHeight = 12;
		var notch = new Rectangle
		{
			Width = 2,
			Height = NotchHeight,
			RadiusX = 1,
			RadiusY = 1,
			Fill = new SolidColorBrush(Colors.White),
			IsHitTestVisible = false,
		};
		Canvas.SetLeft(notch, ToTrackX(Math.Clamp(watchedAt / duration, 0, 1), width) - notch.Width / 2);
		Canvas.SetTop(notch, (track.ActualHeight - NotchHeight) / 2);
		watchedMark.Children.Add(notch);
	}

	/// <summary>Brightens the track up to <paramref name="seconds"/>.</summary>
	public void UpdatePlayed(double seconds, double duration)
	{
		var width = track.ActualWidth;
		var played = duration > 0 && width > 0 ? ToTrackX(Math.Clamp(seconds / duration, 0, 1), width) : 0;
		playedSections.Clip = new RectangleGeometry { Rect = new Rect(0, 0, played, track.ActualHeight) };
	}

	/// <summary>The track's sections, as fractions of the video: the episode where there's no segment.</summary>
	internal static List<(double Start, double End, MediaSegmentType? Type)> GetSections(double duration, IReadOnlyList<MediaChapter> chapters)
	{
		if (duration <= 0 || chapters.Count == 0)
		{
			return [(0, 1, null)];
		}

		List<(double, double, MediaSegmentType?)> sections = [];
		for (var i = 0; i < chapters.Count; i++)
		{
			var start = Math.Clamp(chapters[i].Start.TotalSeconds / duration, 0, 1);
			var end = Math.Clamp((i + 1 < chapters.Count ? chapters[i + 1].Start.TotalSeconds : duration) / duration, 0, 1);
			sections.Add((i == 0 ? 0 : start, end, Enum.TryParse<MediaSegmentType>(chapters[i].Title, out var type) ? type : null));
		}

		return sections;
	}

	// Where a fraction of the video is on the track: where the slider's thumb centre is for it, half a thumb in from
	// each end.
	private static double ToTrackX(double fraction, double width) => ThumbWidth / 2 + fraction * (width - ThumbWidth);

	private static Color SectionColor(MediaSegmentType? type, bool isPlayed)
	{
		var color = type switch
		{
			MediaSegmentType.Opening or MediaSegmentType.Recap => s_openingColor,
			MediaSegmentType.Ending => s_endingColor,
			_ => isPlayed ? (Color)Application.Current.Resources["SystemAccentColorLight2"] : Colors.White,
		};
		// Unplayed is dimmer; the episode's unplayed part is a plain grey track.
		color.A = isPlayed ? (byte)255 : type is null ? (byte)0x5C : (byte)0x80;
		return color;
	}
}
