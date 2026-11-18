using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Media;
using Totoro.Modules.MediaPlayers.Vlc;
using MediaTrack = Totoro.Core.Modules.Media.MediaTrack;

namespace Totoro.ViewModels.Player;

/// <summary>The player's window, playing with libvlc into the video view's swap chain.</summary>
public sealed partial class VlcPlayerViewModel(int volume, PlayerAspectRatio aspectRatio, ILogger logger) : PlayerViewModel(volume, aspectRatio, logger)
{
	public VlcPlayerViewModel(int volume, ILogger logger) : this(volume, PlayerAspectRatio.Fit, logger) { }

	private readonly ILogger _logger = logger;
	private LibVLC? _libVlc;
	private bool _isStarting;
	// Playing again from the end, off the UI thread: closing waits for it before freeing the player.
	private Task _replaying = Task.CompletedTask;

	/// <summary>libvlc's player, for the video view; null until <see cref="InitializeAsync"/>.</summary>
	[ObservableProperty] public partial MediaPlayer? MediaPlayer { get; private set; }

	/// <summary>
	/// Creates libvlc to draw into the video view's swap chain (from its Initialized event). Off the UI thread: loading
	/// libvlc the first time can take seconds (see <see cref="LibVlcLoader"/>).
	/// </summary>
	public async Task InitializeAsync(string[] swapChainOptions)
	{
		if (_libVlc is not null || _isStarting)
		{
			return;
		}

		_isStarting = true;
		if (!LibVlcLoader.IsLoaded)
		{
			ShowStatus("Starting the player…");
		}

		LibVLC libVlc;
		MediaPlayer player;
		try
		{
			(libVlc, player) = await Task.Run(() =>
			{
				LibVlcLoader.Load();
				var libVlc = new LibVLC(enableDebugLogs: false, [.. swapChainOptions, "--no-video-title-show"]);
				return (libVlc, new MediaPlayer(libVlc));
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Couldn't start libvlc");
			ShowStatus("Couldn't start the player.", isError: true);
			// Plays waiting on it go on (and do nothing, there being no player) rather than waiting for ever.
			Initialized.TrySetResult();
			return;
		}

		// Closed while libvlc started: closing found no player to end, so it's ended here.
		if (IsClosed)
		{
			await Task.Run(() =>
			{
				player.Dispose();
				libVlc.Dispose();
			});
			Initialized.TrySetResult();
			return;
		}

		_libVlc = libVlc;
		if (Status == "Starting the player…")
		{
			ShowStatus(null);
		}

		player.TimeChanged += (_, e) => OnUi(() => OnTime(TimeSpan.FromMilliseconds(e.Time)));
		player.LengthChanged += (_, e) => OnUi(() => OnLength(TimeSpan.FromMilliseconds(e.Length)));
		player.Playing += (_, _) => OnUi(OnPlaying);
		player.Paused += (_, _) => OnUi(() => IsPlaying = false);
		player.Buffering += (_, e) => OnUi(() => IsBuffering = e.Cache < 100);
		player.EndReached += (_, _) => OnUi(OnEnded);
		player.EncounteredError += (_, _) => OnUi(() =>
		{
			ShowStatus("Couldn't play the video.", isError: true);
			OnEnded();
		});
		MediaPlayer = player;
		ApplyAspectRatio();
		Initialized.TrySetResult();
	}

	protected override async Task PlayCoreAsync(Uri uri, IEnumerable<string> options, TimeSpan start)
	{
		if (MediaPlayer is not { } player)
		{
			return;
		}

		using var media = new Media(_libVlc!, uri, [.. options, $":start-time={start.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}"]);
		foreach (var (subtitle, i) in Subtitles.Select((x, i) => (x, i)))
		{
			// The highest priority (4) is selected when it loads; the rest are there to pick.
			media.AddSlave(MediaSlaveType.Subtitle, i == 0 ? 4u : 0u, subtitle.Url.AbsoluteUri);
		}

		// Replacing the video stops the last one, which can take a moment (a stream closing): not on the UI thread.
		await Task.Run(() => player.Play(media));
	}

	public override Task SeekAsync(TimeSpan position)
	{
		if (MediaPlayer is { IsSeekable: true } player)
		{
			var time = Math.Clamp((long)position.TotalMilliseconds, 0, Math.Max(0, player.Length - 1000));
			player.Time = time;
			OnTime(TimeSpan.FromMilliseconds(time));
		}
		else if (MediaPlayer is null)
		{
			OnTime(position, report: false);
		}

		return Task.CompletedTask;
	}

	public override bool IsPaused => MediaPlayer?.State == VLCState.Paused;

	public override Task SetPausedAsync(bool paused)
	{
		MediaPlayer?.SetPause(paused);
		return Task.CompletedTask;
	}

	public override Task SetSpeedAsync(double speed)
	{
		MediaPlayer?.SetRate((float)speed);
		return Task.CompletedTask;
	}

	public override IReadOnlyList<MediaTrack> GetTracks()
	{
		if (MediaPlayer is not { } player)
		{
			return [];
		}

		var audio = player.AudioTrack;
		var subtitle = player.Spu;
		var spus = player.SpuDescription;
		// libvlc calls the subtitle files it was given "Track N"; they're its last subtitle tracks, in the order given.
		// shortcut: assumes the video's own subtitle tracks are all listed before them, true of files and HLS once loaded;
		// match by libvlc's media tracks if a stream ever adds its own later.
		var firstFile = spus.Count(x => x.Id >= 0) - Subtitles.Count;
		return
		[
			.. player.AudioTrackDescription.Where(x => x.Id >= 0).Select(x => new MediaTrack(MediaTrackKind.Audio, x.Id, x.Name ?? $"Track {x.Id}", x.Id == audio)),
			.. spus.Where(x => x.Id < 0).Select(x => new MediaTrack(MediaTrackKind.Subtitle, x.Id, "Off", x.Id == subtitle)),
			.. spus.Where(x => x.Id >= 0).Select((x, i) => new MediaTrack(MediaTrackKind.Subtitle, x.Id,
				firstFile >= 0 && i >= firstFile ? Subtitles[i - firstFile].Name : x.Name ?? $"Track {x.Id}", x.Id == subtitle)),
		];
	}

	public override Task SelectTrackAsync(MediaTrackKind kind, int id)
	{
		_ = kind == MediaTrackKind.Audio ? MediaPlayer?.SetAudioTrack(id) : MediaPlayer?.SetSpu(id);
		return Task.CompletedTask;
	}

	public override IReadOnlyList<MediaChapter> GetFileChapters()
	{
		return MediaPlayer?.FullChapterDescriptions() is { } chapters
			? [.. chapters.Select(x => new MediaChapter(x.Name ?? "", TimeSpan.FromMilliseconds(x.TimeOffset)))]
			: [];
	}

	protected override void TogglePlayPause()
	{
		if (MediaPlayer is not { } player)
		{
			return;
		}

		if (player.State == VLCState.Ended)
		{
			_replaying = Task.Run(() =>
			{
				player.Stop();
				player.Play();
			});
			return;
		}

		player.SetPause(player.IsPlaying);
	}

	public override void ApplyAspectRatio()
	{
		if (MediaPlayer is not { } player)
		{
			return;
		}

		player.Scale = 0;
		player.AspectRatio = AspectRatio switch
		{
			PlayerAspectRatio.Stretch when ViewportWidth > 0 && ViewportHeight > 0 =>
				$"{Math.Max(1, (int)Math.Round(ViewportWidth))}:{Math.Max(1, (int)Math.Round(ViewportHeight))}",
			PlayerAspectRatio.Ratio16_9 => "16:9",
			PlayerAspectRatio.Ratio4_3 => "4:3",
			PlayerAspectRatio.Ratio21_9 => "21:9",
			_ => null,
		};
	}

	protected override void ApplyVolume()
	{
		if (MediaPlayer is { } player)
		{
			player.Volume = (int)Volume;
			player.Mute = IsMuted;
		}
	}

	protected override async Task CloseEngineAsync()
	{
		if (MediaPlayer is not { } player)
		{
			return;
		}

		MediaPlayer = null;
		var libVlc = _libVlc;
		_libVlc = null;
		// No new replay starts now (MediaPlayer is null); one under way finishes before the player's freed.
		await _replaying;
		await Task.Run(() =>
		{
			player.Stop();
			player.Dispose();
			libVlc?.Dispose();
		});
	}
}
