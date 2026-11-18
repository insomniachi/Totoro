using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.Core.Modules.Media;
using Totoro.Modules.MediaPlayers.MediaFoundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using MediaTrack = Totoro.Core.Modules.Media.MediaTrack;
using MediaTrackKind = Totoro.Core.Modules.Media.MediaTrackKind;
using WinMediaPlayer = Windows.Media.Playback.MediaPlayer;

namespace Totoro.ViewModels.Player;

/// <summary>
/// The player's window, playing with Windows' own media engine (Media Foundation) into a <see cref="MediaPlayerElement"/>,
/// its own controls off so the app's show instead.
/// </summary>
/// <param name="audioLanguage">The audio track to pick: language codes in order of preference, e.g. "jpn,ja".</param>
/// <param name="subtitleLanguage">The subtitle track to pick, when no subtitle files are given with the video.</param>
public sealed partial class MediaFoundationPlayerViewModel : PlayerViewModel
{
	private readonly WinMediaPlayer _player = new() { AutoPlay = true };
	private readonly string _audioLanguage;
	private readonly string _subtitleLanguage;
	private readonly ILogger _logger;
	private readonly DispatcherQueueTimer _clock;
	private MediaPlayerElement? _element;
	private MediaPlaybackItem? _item;
	// The IDs of the tracks of a Matroska file's subtitles read here (see AddFileSubtitlesAsync).
	private const string FileSubtitlePrefix = "totoro-mkv-";
	// Where and how those show: centred at the bottom, white with a black outline. Without a region Windows puts them at
	// the top left, and a region or style with anything left unset fails the video (DecodingError), so all of it's set.
	private static readonly TimedTextRegion s_cueRegion = new()
	{
		Name = "subtitles",
		Position = new TimedTextPoint { Unit = TimedTextUnit.Percentage, X = 5, Y = 5 },
		Extent = new TimedTextSize { Unit = TimedTextUnit.Percentage, Width = 90, Height = 88 },
		Background = Microsoft.UI.Colors.Transparent,
		WritingMode = TimedTextWritingMode.LeftRightTopBottom,
		DisplayAlignment = TimedTextDisplayAlignment.After,
		LineHeight = new TimedTextDouble { Unit = TimedTextUnit.Percentage, Value = 100 },
		IsOverflowClipped = false,
		Padding = new TimedTextPadding { Unit = TimedTextUnit.Percentage },
		TextWrapping = TimedTextWrapping.Wrap,
		ZIndex = 0,
		ScrollMode = TimedTextScrollMode.Popon,
	};
	private static readonly TimedTextStyle s_cueStyle = new()
	{
		Name = "subtitles",
		FontFamily = "Segoe UI",
		FontSize = new TimedTextDouble { Unit = TimedTextUnit.Percentage, Value = 100 },
		FontWeight = TimedTextWeight.Normal,
		Foreground = Microsoft.UI.Colors.White,
		Background = Microsoft.UI.Colors.Transparent,
		IsBackgroundAlwaysShown = false,
		FlowDirection = TimedTextFlowDirection.LeftToRight,
		LineAlignment = TimedTextLineAlignment.Center,
		OutlineColor = Microsoft.UI.Colors.Black,
		OutlineThickness = new TimedTextDouble { Unit = TimedTextUnit.Percentage, Value = 5 },
		OutlineRadius = new TimedTextDouble { Unit = TimedTextUnit.Percentage, Value = 5 },
	};
	private ReadAheadStream? _stream;
	private bool _hasFileSubtitles;
	private readonly List<ReadAheadStream> _replacedStreams = [];
	private TimeSpan _start;
	private bool _isEnded;
	private bool _isSubtitleChosen;

	public MediaFoundationPlayerViewModel(int volume, PlayerAspectRatio aspectRatio, string audioLanguage, string subtitleLanguage, ILogger logger)
		: base(volume, aspectRatio, logger)
	{
		_audioLanguage = audioLanguage;
		_subtitleLanguage = subtitleLanguage;
		_logger = logger;

		var session = _player.PlaybackSession;
		// PositionChanged comes on seeking, not as it plays: the position is read as libvlc reports it, a few times a second.
		_clock = DispatcherQueue.GetForCurrentThread().CreateTimer();
		_clock.Interval = TimeSpan.FromMilliseconds(250);
		_clock.Tick += (_, _) =>
		{
			if (_item is not null && session.PlaybackState == MediaPlaybackState.Playing)
			{
				OnTime(session.Position);
			}
		};
		_clock.Start();
		session.PositionChanged += (s, _) =>
		{
			var position = s.Position;
			OnUi(() => OnTime(position));
		};
		session.NaturalDurationChanged += (s, _) =>
		{
			var duration = s.NaturalDuration;
			OnUi(() => OnLength(duration));
		};
		session.PlaybackStateChanged += (s, _) =>
		{
			var state = s.PlaybackState;
			OnUi(() =>
			{
				switch (state)
				{
					case MediaPlaybackState.Playing:
						OnPlaying();
						break;
					case MediaPlaybackState.Paused:
						IsPlaying = false;
						break;
					case MediaPlaybackState.Opening or MediaPlaybackState.Buffering:
						IsBuffering = true;
						break;
				}
			});
		};
		_player.MediaOpened += (s, _) =>
		{
			if (_start > TimeSpan.Zero)
			{
				s.PlaybackSession.Position = _start;
			}

			OnUi(() =>
			{
				CloseReplacedStreams();
				PickAudio();
			});
		};
		_player.MediaEnded += (_, _) => OnUi(() =>
		{
			_isEnded = true;
			OnEnded();
		});
		_player.MediaFailed += (_, e) =>
		{
			_logger.LogWarning("Couldn't play the video: {Error} {Message}", e.Error, e.ErrorMessage);
			OnUi(() =>
			{
				ShowStatus("Couldn't play the video.", isError: true);
				OnEnded();
			});
		};
		Initialized.TrySetResult();
	}

	/// <summary>Shows the video in <paramref name="element"/> (the window's).</summary>
	public void Attach(MediaPlayerElement element)
	{
		_element = element;
		element.AreTransportControlsEnabled = false;
		element.SetMediaPlayer(_player);
		ApplyAspectRatio();
	}

	protected override async Task PlayCoreAsync(Uri uri, IEnumerable<string> options, TimeSpan start)
	{
		// A file is downloaded ahead in memory (Windows' own reader stalls on slow hosts); a playlist (HLS, DASH) isn't.
		var file = uri.Scheme is "http" or "https" && !IsPlaylist(uri) ? await ReadAheadStream.OpenAsync(uri) : null;
		try
		{
			var source = file is var (stream, type) ? MediaSource.CreateFromStream(stream.AsRandomAccessStream(), type) : MediaSource.CreateFromUri(uri);
			Play(source, start, hasFileSubtitles: file is { } opened && await AddFileSubtitlesAsync(opened.Stream, source));
		}
		catch
		{
			file?.Stream.Dispose();
			throw;
		}

		// Windows may still be reading the one replaced as it closes it (and reports it failing if it can't): it's
		// closed once the new one's open.
		if (_stream is not null)
		{
			_replacedStreams.Add(_stream);
		}

		_stream = file?.Stream;
	}

	private void CloseReplacedStreams()
	{
		foreach (var stream in _replacedStreams)
		{
			stream.Dispose();
		}

		_replacedStreams.Clear();
	}

	private static bool IsPlaylist(Uri uri)
	{
		return uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Adds a Matroska file's subtitle tracks, read here as it downloads; false if it has none (or isn't one). Windows
	/// reads all of the file before showing a subtitle track of its own, stalling the video meanwhile, so its own are left
	/// off (see <see cref="SubtitleTracks"/>).
	/// </summary>
	private static async Task<bool> AddFileSubtitlesAsync(ReadAheadStream stream, MediaSource source)
	{
		IReadOnlyList<MatroskaSubtitleTrack> tracks;
		try
		{
			// Its header's in its first bytes.
			tracks = await stream.SubtitleTracks.WaitAsync(TimeSpan.FromSeconds(5));
		}
		catch (TimeoutException)
		{
			return false;
		}

		if (tracks.Count == 0)
		{
			return false;
		}


		Dictionary<ulong, TimedMetadataTrack> byNumber = [];
		// The file's default ones first and forced ones (only signs and the like) last: of a language's, the first is picked.
		foreach (var track in tracks.OrderBy(x => x.IsForced).ThenByDescending(x => x.IsDefault))
		{
			var timed = new TimedMetadataTrack(FileSubtitlePrefix + track.Number, track.Language, TimedMetadataKind.Subtitle) { Label = track.Name };
			source.ExternalTimedMetadataTracks.Add(timed);
			byNumber[track.Number] = timed;
		}

		stream.OnSubtitle(cue =>
		{
			if (byNumber.TryGetValue(cue.Track, out var track))
			{
				var timed = new TimedTextCue
				{
					Id = $"{cue.Track}-{cue.Start.Ticks}",
					StartTime = cue.Start,
					Duration = cue.Duration,
					CueRegion = s_cueRegion,
					CueStyle = s_cueStyle,
				};
				foreach (var line in cue.Text.Split('\n'))
				{
					timed.Lines.Add(new TimedTextLine { Text = line });
				}

				track.AddCue(timed);
			}
		});
		return true;
	}

	private void Play(MediaSource source, TimeSpan start, bool hasFileSubtitles)
	{
		_hasFileSubtitles = hasFileSubtitles;
		// shortcut: Windows reads SRT, WebVTT and TTML subtitle files, not ASS; libVLC's player is the one for those.
		foreach (var subtitle in Subtitles)
		{
			var text = TimedTextSource.CreateFromUri(subtitle.Url);
			var name = subtitle.Name;
			// Named as given, which is also how the first one is found to show it (see OnTimedMetadataTracksChanged).
			text.Resolved += (_, e) =>
			{
				foreach (var track in e.Tracks)
				{
					track.Label = name;
				}
			};
			source.ExternalTimedTextSources.Add(text);
		}

		var item = new MediaPlaybackItem(source);
		item.TimedMetadataTracksChanged += (s, _) => OnUi(() => PickSubtitle(s));
		_item = item;
		_start = start;
		_isEnded = false;
		_isSubtitleChosen = false;
		_player.Source = item;
	}

	public override Task SeekAsync(TimeSpan position)
	{
		var session = _player.PlaybackSession;
		if (_item is null)
		{
			OnTime(position, report: false);
		}
		else if (session.CanSeek)
		{
			var time = TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, Math.Max(0, (session.NaturalDuration - TimeSpan.FromSeconds(1)).Ticks)));
			session.Position = time;
			OnTime(time);
		}

		return Task.CompletedTask;
	}

	public override bool IsPaused => _item is not null && !_isEnded && _player.PlaybackSession.PlaybackState == MediaPlaybackState.Paused;

	public override Task SetPausedAsync(bool paused)
	{
		if (paused)
		{
			_player.Pause();
		}
		else
		{
			_player.Play();
		}

		return Task.CompletedTask;
	}

	public override Task SetSpeedAsync(double speed)
	{
		_player.PlaybackSession.PlaybackRate = speed;
		return Task.CompletedTask;
	}

	public override IReadOnlyList<MediaTrack> GetTracks()
	{
		if (_item is not { } item)
		{
			return [];
		}

		var subtitles = SubtitleTracks(item).ToList();
		return
		[
			.. item.AudioTracks.Select((x, i) => new MediaTrack(MediaTrackKind.Audio, i, TrackName(x.Label, x.Language, i), i == item.AudioTracks.SelectedIndex)),
			new MediaTrack(MediaTrackKind.Subtitle, -1, "Off", !subtitles.Any(x => item.TimedMetadataTracks.GetPresentationMode((uint)x.Index) == TimedMetadataTrackPresentationMode.PlatformPresented)),
			.. subtitles.Select(x => new MediaTrack(MediaTrackKind.Subtitle, x.Index, TrackName(x.Track.Label, x.Track.Language, x.Index),
				item.TimedMetadataTracks.GetPresentationMode((uint)x.Index) == TimedMetadataTrackPresentationMode.PlatformPresented)),
		];
	}

	public override Task SelectTrackAsync(MediaTrackKind kind, int id)
	{
		if (_item is not { } item)
		{
			return Task.CompletedTask;
		}

		if (kind == MediaTrackKind.Audio)
		{
			if (id >= 0 && id < item.AudioTracks.Count)
			{
				item.AudioTracks.SelectedIndex = id;
			}
		}
		else
		{
			ShowSubtitle(item, id);
		}

		return Task.CompletedTask;
	}

	// shortcut: a file's own chapters aren't read (Windows offers no API for them); the opening and ending still come from outside.
	public override IReadOnlyList<MediaChapter> GetFileChapters() => [];

	protected override void TogglePlayPause()
	{
		if (_item is null)
		{
			return;
		}

		if (_isEnded)
		{
			_isEnded = false;
			_player.PlaybackSession.Position = TimeSpan.Zero;
			_player.Play();
			return;
		}

		if (_player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
		{
			_player.Pause();
		}
		else
		{
			_player.Play();
		}
	}

	public override void ApplyAspectRatio()
	{
		if (_element is not { } element)
		{
			return;
		}

		// The element fits the video to itself (Uniform) or fills itself (Fill); a ratio of its own is the element sized to it.
		double? ratio = AspectRatio switch
		{
			PlayerAspectRatio.Ratio16_9 => 16 / 9.0,
			PlayerAspectRatio.Ratio4_3 => 4 / 3.0,
			PlayerAspectRatio.Ratio21_9 => 21 / 9.0,
			_ => null,
		};
		element.Stretch = AspectRatio == PlayerAspectRatio.Fit ? Stretch.Uniform : Stretch.Fill;
		if (ratio is { } r && ViewportWidth > 0 && ViewportHeight > 0)
		{
			element.Width = Math.Min(ViewportWidth, ViewportHeight * r);
			element.Height = element.Width / r;
		}
		else
		{
			element.Width = double.NaN;
			element.Height = double.NaN;
		}
	}

	protected override void ApplyVolume()
	{
		_player.Volume = Volume / 100;
		_player.IsMuted = IsMuted;
	}

	protected override Task CloseEngineAsync()
	{
		_clock.Stop();
		_element?.SetMediaPlayer(null);
		_element = null;
		_item = null;
		_player.Pause();
		_player.Source = null;
		_player.Dispose();
		_stream?.Dispose();
		_stream = null;
		CloseReplacedStreams();
		return Task.CompletedTask;
	}

	private void PickAudio()
	{
		if (_item is { } item && IndexOf([.. item.AudioTracks.Select(x => x.Language)], _audioLanguage) is >= 0 and var index)
		{
			item.AudioTracks.SelectedIndex = index;
		}
	}

	// As subtitle tracks come (the files given resolve after the video opens): the first file given if there are any,
	// otherwise the preferred language.
	private void PickSubtitle(MediaPlaybackItem item)
	{
		if (_isSubtitleChosen || !ReferenceEquals(item, _item))
		{
			return;
		}

		var tracks = SubtitleTracks(item).ToList();
		var match = Subtitles.Count > 0
			? tracks.FindIndex(x => x.Track.Label == Subtitles[0].Name)
			: IndexOf([.. tracks.Select(x => x.Track.Language)], _subtitleLanguage);
		if (match >= 0)
		{
			_isSubtitleChosen = true;
			ShowSubtitle(item, tracks[match].Index);
		}
	}

	private void ShowSubtitle(MediaPlaybackItem item, int index)
	{
		foreach (var (_, i) in SubtitleTracks(item))
		{
			item.TimedMetadataTracks.SetPresentationMode((uint)i, i == index ? TimedMetadataTrackPresentationMode.PlatformPresented : TimedMetadataTrackPresentationMode.Disabled);
		}
	}

	// With a Matroska file's subtitles read here, its tracks as Windows reads them are left out (and off): showing one
	// has Windows read all of the file first. Those of the subtitle files given stay.
	private IEnumerable<(TimedMetadataTrack Track, int Index)> SubtitleTracks(MediaPlaybackItem item)
	{
		return item.TimedMetadataTracks
			.Select((x, i) => (x, i))
			.Where(x => x.x.TimedMetadataKind is TimedMetadataKind.Subtitle or TimedMetadataKind.Caption)
			.Where(x => !_hasFileSubtitles || x.x.Id.StartsWith(FileSubtitlePrefix, StringComparison.Ordinal) || Subtitles.Any(s => s.Name == x.x.Label));
	}

	private static string TrackName(string? label, string? language, int index)
	{
		return !string.IsNullOrEmpty(label) ? label : !string.IsNullOrEmpty(language) ? language : $"Track {index + 1}";
	}

	/// <summary>The first of <paramref name="languages"/> matching the earliest of <paramref name="preference"/>'s codes, e.g. "jpn,ja"; -1 if none.</summary>
	internal static int IndexOf(IReadOnlyList<string?> languages, string preference)
	{
		foreach (var code in preference.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
		{
			for (var i = 0; i < languages.Count; i++)
			{
				if (languages[i] is { } language && (language.Equals(code, StringComparison.OrdinalIgnoreCase) || language.StartsWith(code + "-", StringComparison.OrdinalIgnoreCase)))
				{
					return i;
				}
			}
		}

		return -1;
	}
}
