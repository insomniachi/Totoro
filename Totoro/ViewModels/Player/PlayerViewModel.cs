using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Totoro.Core.Modules.Media;
using Totoro.Modules.MediaPlayers.Vlc;
using MediaTrack = Totoro.Core.Modules.Media.MediaTrack;

namespace Totoro.ViewModels.Player;

/// <summary>
/// The built-in player's window: plays a video with libvlc and holds what its controls and overlays show (the seek bar
/// with the opening and ending marked, the skip button, the next episode...). Created on the UI thread; libvlc's
/// events are brought to it, so properties and events change there.
/// </summary>
public sealed partial class PlayerViewModel(int volume, ILogger logger) : ObservableObject
{
	// An opening's length, give or take: what the "skip ahead" button skips.
	public static readonly TimeSpan OpeningLength = TimeSpan.FromSeconds(85);

	// How long the button over the video (e.g. "Skip opening") stays, and how often its bar moves on.
	private static readonly TimeSpan s_offerDuration = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan s_offerTick = TimeSpan.FromMilliseconds(30);

	private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();
	private readonly TaskCompletionSource _initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly SemaphoreSlim _loading = new(1, 1);
	private LibVLC? _libVlc;
	private TaskCompletionSource<bool>? _offer;
	private TaskCompletionSource<bool>? _next;
	private CancellationTokenSource? _message;
	private bool _isClosed;
	private bool _isStarting;

	public event EventHandler<TimeSpan>? DurationChanged;

	public event EventHandler<TimeSpan>? PositionChanged;

	/// <summary>Raised when the video ends, or can't be played.</summary>
	public event EventHandler? Ended;

	/// <summary>libvlc's player, for the video view; null until <see cref="Initialize"/>.</summary>
	[ObservableProperty] public partial MediaPlayer? MediaPlayer { get; private set; }

	[ObservableProperty] public partial string Title { get; private set; } = "";

	[ObservableProperty] public partial bool IsPlaying { get; private set; }

	/// <summary>Whether it's waiting for the video (starting, or after seeking).</summary>
	[ObservableProperty] public partial bool IsBuffering { get; private set; }

	/// <summary>The video's length, in seconds; 0 until it's known.</summary>
	[ObservableProperty] public partial double Duration { get; private set; }

	/// <summary>Where playback is, in seconds.</summary>
	[ObservableProperty] public partial double Position { get; private set; }

	/// <summary>Where playback is, e.g. "12:34"; empty until the length is known.</summary>
	[ObservableProperty] public partial string PositionText { get; private set; } = "";

	/// <summary>The video's length, e.g. "24:01"; empty until it's known.</summary>
	[ObservableProperty] public partial string DurationText { get; private set; } = "";

	/// <summary>From 0 to 100.</summary>
	[ObservableProperty] public partial double Volume { get; set; } = volume;

	[ObservableProperty] public partial bool IsMuted { get; set; }

	[ObservableProperty] public partial bool IsFullScreen { get; set; }

	/// <summary>The opening, ending... as chapters, marked on the seek bar.</summary>
	[ObservableProperty] public partial IReadOnlyList<MediaChapter> Chapters { get; private set; } = [];

	/// <summary>A short message over the video, e.g. "Skipped the opening".</summary>
	[ObservableProperty] public partial string Message { get; private set; } = "";

	[ObservableProperty] public partial bool IsMessageVisible { get; private set; }

	/// <summary>The button over the video, e.g. "Skip opening".</summary>
	[ObservableProperty] public partial string OfferText { get; private set; } = "";

	[ObservableProperty] public partial bool IsOfferVisible { get; private set; }

	/// <summary>How much of its time on screen the button over the video has had, from 0 to 1; it goes at 1.</summary>
	[ObservableProperty] public partial double OfferProgress { get; private set; }

	/// <summary>The next episode's card: its title, e.g. "Episode 6".</summary>
	[ObservableProperty] public partial string NextTitle { get; private set; } = "";

	[ObservableProperty] public partial bool IsNextVisible { get; private set; }

	/// <summary>e.g. "Playing in 5…"; empty when it waits to be asked.</summary>
	[ObservableProperty] public partial string NextCountdown { get; private set; } = "";

	/// <summary>What's happening while there's no video, e.g. "Getting episode 6…".</summary>
	[ObservableProperty] public partial string Status { get; private set; } = "";

	[ObservableProperty] public partial bool IsStatusVisible { get; private set; }

	[ObservableProperty] public partial bool IsStatusError { get; private set; }

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

		(_libVlc, var player) = await Task.Run(() =>
		{
			LibVlcLoader.Load();
			var libVlc = new LibVLC(enableDebugLogs: false, [.. swapChainOptions, "--no-video-title-show"]);
			return (libVlc, new MediaPlayer(libVlc));
		});
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
		_initialized.TrySetResult();
	}

	/// <summary>Plays <paramref name="uri"/> from <paramref name="start"/>, replacing what's playing.</summary>
	/// <param name="options">libvlc media options, e.g. ":http-referrer=...".</param>
	public async Task PlayAsync(Uri uri, string title, IEnumerable<string> options, TimeSpan start)
	{
		Title = title;
		ResetForNewVideo();
		IsBuffering = true;

		await _initialized.Task;
		await _loading.WaitAsync();
		try
		{
			if (_isClosed || MediaPlayer is not { } player)
			{
				return;
			}

			using var media = new Media(_libVlc!, uri, [.. options, $":start-time={start.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}"]);
			// Replacing the video stops the last one, which can take a moment (a stream closing): not on the UI thread.
			await Task.Run(() => player.Play(media));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Couldn't play {Uri}", uri);
			ShowStatus("Couldn't play the video.", isError: true);
		}
		finally
		{
			_loading.Release();
		}
	}

	public Task SeekAsync(TimeSpan position)
	{
		if (MediaPlayer is { IsSeekable: true } player)
		{
			var time = Math.Clamp((long)position.TotalMilliseconds, 0, Math.Max(0, player.Length - 1000));
			player.Time = time;
			// Shown straight away rather than when libvlc next reports it.
			OnTime(TimeSpan.FromMilliseconds(time), report: false);
		}

		return Task.CompletedTask;
	}

	public bool IsPaused => MediaPlayer?.State == VLCState.Paused;

	public Task SetPausedAsync(bool paused)
	{
		MediaPlayer?.SetPause(paused);
		return Task.CompletedTask;
	}

	public IReadOnlyList<MediaTrack> GetTracks()
	{
		if (MediaPlayer is not { } player)
		{
			return [];
		}

		var audio = player.AudioTrack;
		var subtitle = player.Spu;
		return
		[
			.. player.AudioTrackDescription.Where(x => x.Id >= 0).Select(x => new MediaTrack(MediaTrackKind.Audio, x.Id, x.Name ?? $"Track {x.Id}", x.Id == audio)),
			.. player.SpuDescription.Select(x => new MediaTrack(MediaTrackKind.Subtitle, x.Id, x.Id < 0 ? "Off" : x.Name ?? $"Track {x.Id}", x.Id == subtitle)),
		];
	}

	public Task SelectTrackAsync(MediaTrackKind kind, int id)
	{
		_ = kind == MediaTrackKind.Audio ? MediaPlayer?.SetAudioTrack(id) : MediaPlayer?.SetSpu(id);
		return Task.CompletedTask;
	}

	public Task SetChaptersAsync(IReadOnlyList<MediaChapter> chapters)
	{
		Chapters = chapters;
		return Task.CompletedTask;
	}

	public async Task ShowMessageAsync(string text, TimeSpan duration)
	{
		_message?.Cancel();
		var message = _message = new CancellationTokenSource();
		Message = text;
		IsMessageVisible = true;
		try
		{
			await Task.Delay(duration, message.Token);
			IsMessageVisible = false;
		}
		catch (OperationCanceledException)
		{
			// Replaced by another message.
		}
	}

	/// <inheritdoc cref="IInteractiveMediaPlayer.OfferAsync"/>
	public async Task<bool> OfferAsync(string text, CancellationToken ct)
	{
		_offer?.TrySetResult(false);
		var offer = _offer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		OfferText = text;
		OfferProgress = 0;
		IsOfferVisible = true;
		await using var registration = ct.Register(() => offer.TrySetResult(false));
		try
		{
			// Up for a few seconds of playback (it waits while paused), its bar filling meanwhile.
			var shown = TimeSpan.Zero;
			var last = DateTime.UtcNow;
			while (!offer.Task.IsCompleted)
			{
				await Task.WhenAny(offer.Task, Task.Delay(s_offerTick, CancellationToken.None));
				var now = DateTime.UtcNow;
				if (IsPlaying)
				{
					shown += now - last;
				}

				last = now;
				OfferProgress = Math.Min(1, shown / s_offerDuration);
				if (shown >= s_offerDuration)
				{
					offer.TrySetResult(false);
				}
			}

			return await offer.Task;
		}
		finally
		{
			if (ReferenceEquals(_offer, offer))
			{
				_offer = null;
				IsOfferVisible = false;
			}
		}
	}

	/// <inheritdoc cref="IInteractiveMediaPlayer.OfferNextAsync"/>
	public async Task<bool> OfferNextAsync(string title, TimeSpan? countdown, CancellationToken ct)
	{
		_next?.TrySetResult(false);
		var next = _next = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		NextTitle = title;
		NextCountdown = "";
		IsNextVisible = true;
		await using var registration = ct.Register(() => next.TrySetResult(false));
		try
		{
			for (var left = countdown ?? TimeSpan.Zero; countdown is not null && !next.Task.IsCompleted; left -= TimeSpan.FromSeconds(1))
			{
				if (left <= TimeSpan.Zero)
				{
					next.TrySetResult(true);
					break;
				}

				NextCountdown = $"Playing in {left.TotalSeconds:0}…";
				await Task.WhenAny(next.Task, Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None));
			}

			return await next.Task;
		}
		finally
		{
			if (ReferenceEquals(_next, next))
			{
				_next = null;
				IsNextVisible = false;
			}
		}
	}

	/// <inheritdoc cref="IInteractiveMediaPlayer.ShowStatus"/>
	public void ShowStatus(string? text, bool isError = false)
	{
		Status = text ?? "";
		IsStatusError = isError;
		IsStatusVisible = text is not null;
	}

	/// <summary>Stops playing and lets go of libvlc, when the window closes.</summary>
	public async Task CloseAsync()
	{
		_isClosed = true;
		_offer?.TrySetResult(false);
		_next?.TrySetResult(false);
		_message?.Cancel();
		if (MediaPlayer is not { } player)
		{
			return;
		}

		MediaPlayer = null;
		var libVlc = _libVlc;
		_libVlc = null;
		await _loading.WaitAsync();
		await Task.Run(() =>
		{
			player.Stop();
			player.Dispose();
			libVlc?.Dispose();
		});
	}

	[RelayCommand]
	private void PlayPause()
	{
		if (MediaPlayer is not { } player)
		{
			return;
		}

		// Played to the end: play it again from the start.
		if (player.State == VLCState.Ended)
		{
			_ = Task.Run(() =>
			{
				player.Stop();
				player.Play();
			});
			return;
		}

		player.SetPause(player.IsPlaying);
	}

	/// <summary>Jumps by a number of seconds (negative to go back).</summary>
	[RelayCommand]
	private Task SeekBy(double seconds) => SeekAsync(TimeSpan.FromSeconds(Position + seconds));

	/// <summary>Skips an opening's length, for when the opening isn't known.</summary>
	[RelayCommand]
	private Task SkipAhead() => SeekBy(OpeningLength.TotalSeconds);

	[RelayCommand]
	private void ToggleMute() => IsMuted = !IsMuted;

	[RelayCommand]
	private void ToggleFullScreen() => IsFullScreen = !IsFullScreen;

	/// <summary>Takes up the button over the video, e.g. skips the opening.</summary>
	[RelayCommand]
	private void AcceptOffer() => _offer?.TrySetResult(true);

	/// <summary>Plays the next episode now.</summary>
	[RelayCommand]
	private void PlayNext() => _next?.TrySetResult(true);

	[RelayCommand]
	private void DismissNext() => _next?.TrySetResult(false);

	/// <summary>Whether there's a button over the video to take up, e.g. for a key that does it.</summary>
	public bool HasOffer => _offer is not null;

	/// <summary>Raised when the user picks an episode to play, by its number: from the list, or previous or next.</summary>
	public event EventHandler<int>? EpisodeRequested;

	/// <summary>The anime's episodes, for the episode list.</summary>
	[ObservableProperty] public partial IReadOnlyList<EpisodeListItem> Episodes { get; private set; } = [];

	[ObservableProperty] public partial bool IsEpisodeListOpen { get; set; }

	[ObservableProperty] public partial bool HasPreviousEpisode { get; private set; }

	[ObservableProperty] public partial bool HasNextEpisode { get; private set; }

	/// <inheritdoc cref="IInteractiveMediaPlayer.SetEpisodes"/>
	public void SetEpisodes(IReadOnlyList<PlayerEpisode> episodes)
	{
		Episodes = [.. episodes.Select(x => new EpisodeListItem(x))];
		var current = episodes.FirstOrDefault(x => x.IsCurrent)?.Number;
		HasPreviousEpisode = episodes.Any(x => x.Number == current - 1);
		HasNextEpisode = episodes.Any(x => x.Number == current + 1);
	}

	[RelayCommand]
	private void ToggleEpisodeList() => IsEpisodeListOpen = !IsEpisodeListOpen;

	[RelayCommand]
	private void PlayEpisode(EpisodeListItem? episode)
	{
		IsEpisodeListOpen = false;
		if (episode is { IsCurrent: false })
		{
			EpisodeRequested?.Invoke(this, episode.Number);
		}
	}

	[RelayCommand]
	private void PreviousEpisode() => RequestRelative(-1);

	/// <summary>Plays the next episode: takes up the "Up next" offer if it's showing.</summary>
	[RelayCommand]
	private void NextEpisode()
	{
		if (_next is not null)
		{
			_next.TrySetResult(true);
			return;
		}

		RequestRelative(1);
	}

	private void RequestRelative(int offset)
	{
		if (Episodes.FirstOrDefault(x => x.IsCurrent) is { } current && Episodes.Any(x => x.Number == current.Number + offset))
		{
			EpisodeRequested?.Invoke(this, current.Number + offset);
		}
	}

	partial void OnVolumeChanged(double value)
	{
		if (MediaPlayer is { } player)
		{
			player.Volume = (int)value;
		}

		if (value > 0)
		{
			IsMuted = false;
		}
	}

	partial void OnIsMutedChanged(bool value)
	{
		if (MediaPlayer is { } player)
		{
			player.Mute = value;
		}
	}

	private void OnPlaying()
	{
		IsPlaying = true;
		IsBuffering = false;
		// The volume only applies once there's audio.
		if (MediaPlayer is { } player)
		{
			player.Volume = (int)Volume;
			player.Mute = IsMuted;
		}
	}

	private void OnTime(TimeSpan time, bool report = true)
	{
		Position = time.TotalSeconds;
		IsBuffering = false;
		UpdateTimeText();
		if (report)
		{
			PositionChanged?.Invoke(this, time);
		}
	}

	private void OnLength(TimeSpan length)
	{
		Duration = length.TotalSeconds;
		UpdateTimeText();
		DurationChanged?.Invoke(this, length);
	}

	private void OnEnded()
	{
		IsPlaying = false;
		IsBuffering = false;
		Ended?.Invoke(this, EventArgs.Empty);
	}

	private void ResetForNewVideo()
	{
		_offer?.TrySetResult(false);
		_next?.TrySetResult(false);
		Position = 0;
		Duration = 0;
		Chapters = [];
		UpdateTimeText();
		ShowStatus(null);
	}

	private void UpdateTimeText()
	{
		var duration = TimeSpan.FromSeconds(Duration);
		PositionText = Duration > 0 ? Format(TimeSpan.FromSeconds(Position), duration) : "";
		DurationText = Duration > 0 ? Format(duration, duration) : "";
	}

	/// <summary>The opening, ending or recap <paramref name="seconds"/> is in; null if it's in none (or they aren't known).</summary>
	public MediaSegmentType? SegmentAt(double seconds)
	{
		for (var i = Chapters.Count - 1; i >= 0; i--)
		{
			if (Chapters[i].Start.TotalSeconds <= seconds)
			{
				return Enum.TryParse<MediaSegmentType>(Chapters[i].Title, out var type) ? type : null;
			}
		}

		return null;
	}

	/// <summary>m:ss, or h:mm:ss for an hour or longer (a movie).</summary>
	public static string Format(TimeSpan time, TimeSpan duration)
	{
		return time.ToString(duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
	}

	// libvlc raises its events on its own threads, which mustn't call back into it either.
	private void OnUi(Action action) => _ui.TryEnqueue(() =>
	{
		if (!_isClosed)
		{
			action();
		}
	});
}

/// <summary>An episode in the player's episode list, as it's shown.</summary>
public sealed record EpisodeListItem(PlayerEpisode Episode)
{
	public int Number => Episode.Number;

	public string NumberText => Episode.Number.ToString();

	public string Title => Episode.Title;

	public string Details => Episode.Details;

	public string Overview => Episode.Overview;

	/// <summary>Its thumbnail's URL; empty (never null, for x:Bind) when there's none.</summary>
	public string Image => Episode.Image ?? "";

	public bool IsWatched => Episode.IsWatched;

	public bool IsCurrent => Episode.IsCurrent;

	public double Progress => Episode.Progress;

	/// <summary>Whether it was left part-way through (the one playing shows where it is on the seek bar instead).</summary>
	public bool HasProgress => Episode.Progress > 0 && !Episode.IsCurrent;

	/// <summary>Its accessible name, e.g. "Episode 5, Title, playing".</summary>
	public string Name => $"Episode {Number}, {Title}{(IsCurrent ? ", playing" : IsWatched ? ", watched" : "")}";
}
