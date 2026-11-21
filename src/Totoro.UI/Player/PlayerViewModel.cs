using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Totoro.Core.Modules.Media;
using MediaTrack = Totoro.Core.Modules.Media.MediaTrack;
using SubtitleTrack = Totoro.Core.Modules.Streaming.SubtitleTrack;

namespace Totoro.UI.Player;

/// <summary>
/// The built-in players' window: plays a video and holds what its controls and overlays show (the seek bar with the
/// opening and ending marked, the skip button, the next episode...). What plays the video is the derived class's (mpv,
/// or Windows' own). Created on the UI thread; the engine's events are brought to it, so properties and events change there.
/// </summary>
public abstract partial class PlayerViewModel(int volume, PlayerAspectRatio aspectRatio, ILogger logger) : ObservableObject
{
	// An opening's length, give or take: what the "skip ahead" button skips.
	public static readonly TimeSpan OpeningLength = TimeSpan.FromSeconds(85);

	// How long the button over the video (e.g. "Skip opening") stays, and how often its bar moves on.
	private static readonly TimeSpan s_offerDuration = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan s_offerTick = TimeSpan.FromMilliseconds(30);

	private readonly DispatcherQueue? _ui = TryGetDispatcherQueue();
	// Set once the engine can play: playing waits for it.
	protected readonly TaskCompletionSource Initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly SemaphoreSlim _loading = new(1, 1);
	private TaskCompletionSource<bool>? _offer;
	private TaskCompletionSource<bool>? _next;
	private CancellationTokenSource? _message;
	private bool _isClosed;

	/// <summary>Whether <see cref="CloseAsync"/> has been called.</summary>
	protected bool IsClosed => _isClosed;

	// The subtitle files given with what's playing, to name their tracks.
	private IReadOnlyList<SubtitleTrack> _subtitles = [];

	protected IReadOnlyList<SubtitleTrack> Subtitles => _subtitles;

	public event EventHandler<TimeSpan>? DurationChanged;

	public event EventHandler<TimeSpan>? PositionChanged;

	/// <summary>Raised when the video ends, or can't be played.</summary>
	public event EventHandler? Ended;

	[ObservableProperty] public partial string Title { get; private set; } = "";

	/// <summary>The anime's title logo's URL, under the title bar; empty (never null, for x:Bind) when there's none.</summary>
	[ObservableProperty] public partial string Logo { get; set; } = "";

	[ObservableProperty] public partial bool IsPlaying { get; protected set; }

	// Made when playback first starts, on the UI thread: it can't be activated off one (as in tests), and isn't needed before.
	private Windows.System.Display.DisplayRequest? _displayRequest;

	/// <summary>Whether it's waiting for the video (starting, or after seeking).</summary>
	[ObservableProperty] public partial bool IsBuffering { get; protected set; }

	/// <summary>The video's length, in seconds; 0 until it's known.</summary>
	[ObservableProperty] public partial double Duration { get; private set; }

	/// <summary>Where playback is, in seconds.</summary>
	[ObservableProperty] public partial double Position { get; private set; }

	/// <summary>Where playback is, e.g. "12:34"; empty until the length is known.</summary>
	[ObservableProperty] public partial string PositionText { get; private set; } = "";

	/// <summary>The video's length, e.g. "24:01"; empty until it's known.</summary>
	[ObservableProperty] public partial string DurationText { get; private set; } = "";

	/// <summary>Where the episode counts as watched, in seconds, marked on the seek bar; -1 when it isn't known.</summary>
	[ObservableProperty] public partial double WatchedAt { get; private set; } = -1;

	/// <summary>Whether the episode has been marked watched: a check by its length.</summary>
	[ObservableProperty] public partial bool IsWatched { get; private set; }

	/// <inheritdoc cref="IInteractiveMediaPlayer.SetWatchedMark"/>
	public void SetWatchedMark(TimeSpan? countsAt, bool isWatched)
	{
		WatchedAt = countsAt?.TotalSeconds ?? -1;
		IsWatched = isWatched;
	}

	// How long the tick stays once the lists are saved.
	private static readonly TimeSpan s_savedDuration = TimeSpan.FromSeconds(1.5);
	// Which save the indicator shows: one started meanwhile takes it over.
	private int _saving;

	/// <summary>Whether the save indicator (top right) shows: while saving to the lists, and briefly after.</summary>
	[ObservableProperty] public partial bool IsSaveIndicatorVisible { get; private set; }

	/// <summary>Whether it's saving to the lists: a spinning ring, which turns into a tick when it's done.</summary>
	[ObservableProperty] public partial bool IsSaving { get; private set; }

	/// <summary>Whether the lists have been saved to: the tick the ring turns into.</summary>
	[ObservableProperty] public partial bool IsSaved { get; private set; }

	/// <inheritdoc cref="IInteractiveMediaPlayer.ShowSaving"/>
	public async Task ShowSavingAsync(Task saving)
	{
		var id = ++_saving;
		IsSaved = false;
		IsSaving = true;
		IsSaveIndicatorVisible = true;
		bool saved;
		try
		{
			await saving;
			saved = true;
		}
		catch
		{
			// Said by whoever saved; it just goes.
			saved = false;
		}

		if (id != _saving)
		{
			return;
		}

		IsSaving = false;
		IsSaved = saved;
		if (saved)
		{
			await Task.Delay(s_savedDuration);
		}

		if (id == _saving)
		{
			IsSaveIndicatorVisible = false;
		}
	}

	/// <summary>From 0 to 100.</summary>
	[ObservableProperty] public partial double Volume { get; set; } = volume;

	[ObservableProperty] public partial bool IsMuted { get; set; }

	[ObservableProperty] public partial bool IsFullScreen { get; set; }

	[ObservableProperty] public partial PlayerAspectRatio AspectRatio { get; set; } = aspectRatio;

	public double ViewportWidth { get; private set; }

	public double ViewportHeight { get; private set; }

	[ObservableProperty] public partial bool IsSkipButtonVisible { get; private set; } = true;

	[ObservableProperty] public partial string SkipButtonText { get; private set; } = "+85s";

	[ObservableProperty] public partial string SkipButtonToolTip { get; private set; } = "Skip 85 seconds, an opening's length";

	/// <summary>
	/// Whether it's a short video (e.g. an opening, from the Music tab) rather than an episode: the controls are just play,
	/// the seek bar, volume and full screen.
	/// </summary>
	[ObservableProperty] public partial bool IsClip { get; private set; }

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

	/// <summary>The next episode's own title, if known (otherwise <see cref="NextTitle"/> again).</summary>
	[ObservableProperty] public partial string NextName { get; private set; } = "";

	/// <summary>The next episode's thumbnail URL; empty (never null, for x:Bind) when there's none.</summary>
	[ObservableProperty] public partial string NextImage { get; private set; } = "";

	[ObservableProperty] public partial bool IsNextVisible { get; private set; }

	/// <summary>e.g. "Playing in 5â€¦"; empty when it waits to be asked.</summary>
	[ObservableProperty] public partial string NextCountdown { get; private set; } = "";

	/// <summary>What's happening while there's no video, e.g. "Getting episode 6â€¦".</summary>
	[ObservableProperty] public partial string Status { get; private set; } = "";

	[ObservableProperty] public partial bool IsStatusVisible { get; private set; }

	[ObservableProperty] public partial bool IsStatusError { get; private set; }

	/// <summary>Plays <paramref name="uri"/> from <paramref name="start"/>, replacing what's playing.</summary>
	/// <param name="options">Engine-specific options (libvlc's media options, e.g. ":http-referrer=..."); ignored by engines without them.</param>
	/// <param name="isClip">A short video (e.g. an opening) rather than an episode: only the simple controls show, and no episodes.</param>
	/// <param name="subtitles">Subtitle files to load beside it, the first one shown.</param>
	public async Task PlayAsync(Uri uri, string title, IEnumerable<string> options, TimeSpan start, bool isClip = false, IReadOnlyList<SubtitleTrack>? subtitles = null)
	{
		Title = title;
		IsClip = isClip;
		_subtitles = subtitles ?? [];
		ResetForNewVideo();
		if (isClip)
		{
			EpisodeList.SetEpisodes([]);
		}

		IsBuffering = true;

		await Initialized.Task;
		await _loading.WaitAsync();
		try
		{
			if (_isClosed)
			{
				return;
			}

			await PlayCoreAsync(uri, options, start);
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

	/// <summary>Has the engine play <paramref name="uri"/> from <paramref name="start"/>, with <see cref="Subtitles"/>.</summary>
	protected abstract Task PlayCoreAsync(Uri uri, IEnumerable<string> options, TimeSpan start);

	/// <summary>
	/// Seeks, and shows and reports the new position straight away rather than when the engine next reports it, which it
	/// may not while paused (watching together would tell the room the old position). Before there's a video, it's only shown.
	/// </summary>
	public abstract Task SeekAsync(TimeSpan position);

	public abstract bool IsPaused { get; }

	public abstract Task SetPausedAsync(bool paused);

	public abstract Task SetSpeedAsync(double speed);

	public abstract IReadOnlyList<MediaTrack> GetTracks();

	public abstract Task SelectTrackAsync(MediaTrackKind kind, int id);

	/// <summary>The chapters in the video file itself (<see cref="Chapters"/> are the ones set from outside).</summary>
	public abstract IReadOnlyList<MediaChapter> GetFileChapters();

	/// <summary>
	/// The upscaling modes to pick from (Anime4K's), "Off" first; none when the engine has none (only mpv's shaders do).
	/// </summary>
	public virtual IReadOnlyList<string> ShaderModes => [];

	/// <summary>What the mode at <paramref name="index"/> in <see cref="ShaderModes"/> does and when to use it; null if unknown.</summary>
	public virtual string? GetShaderModeDescription(int index) => null;

	/// <summary>The index in <see cref="ShaderModes"/> of the one in use; -1 if it's none of them.</summary>
	public virtual int GetShaderMode() => -1;

	public virtual void SelectShaderMode(int index) { }

	/// <summary>Raises the subtitles by <paramref name="fraction"/> of the video area's height, clear of the controls (0: back down).</summary>
	public virtual void SetSubtitleLift(double fraction) { }

	/// <summary>
	/// The view the engine draws the video in, put under the window's controls; null for the app's own engines, whose
	/// views the window makes itself. Called once, on the UI thread, as the window opens.
	/// </summary>
	public virtual UIElement? CreateVideoView() => null;

	/// <summary>Plays or pauses; played to the end, plays it again from the start.</summary>
	protected abstract void TogglePlayPause();

	/// <summary>Has the engine show the video at <see cref="AspectRatio"/>.</summary>
	public abstract void ApplyAspectRatio();

	/// <summary>Has the engine play at <see cref="Volume"/>, muted if <see cref="IsMuted"/>.</summary>
	protected abstract void ApplyVolume();

	/// <summary>Stops playing and lets go of the engine.</summary>
	protected abstract Task CloseEngineAsync();

	public Task SetChaptersAsync(IReadOnlyList<MediaChapter> chapters)
	{
		Chapters = chapters;
		UpdateSkipButtonState();
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
		// Its thumbnail and title, from the episode list.
		var current = EpisodeList.Episodes.FirstOrDefault(x => x.IsCurrent);
		var upNext = current is null ? null : EpisodeList.Episodes.FirstOrDefault(x => x.Number == current.Number + 1);
		NextName = upNext?.Title ?? title;
		NextImage = upNext?.Image ?? "";
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

				NextCountdown = $"Playing in {left.TotalSeconds:0}â€¦";
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

	/// <summary>Stops playing and lets go of the engine, when the window closes.</summary>
	public async Task CloseAsync()
	{
		_isClosed = true;
		_offer?.TrySetResult(false);
		_next?.TrySetResult(false);
		_message?.Cancel();
		IsPlaying = false;
		// Not while a video is being started.
		await _loading.WaitAsync();
		await CloseEngineAsync();
	}

	[RelayCommand]
	private void PlayPause() => TogglePlayPause();

	/// <summary>Jumps by a number of seconds (negative to go back).</summary>
	[RelayCommand]
	private Task SeekBy(double seconds) => SeekAsync(TimeSpan.FromSeconds(Position + seconds));

	/// <summary>Skips an opening or ending if currently playing one, or an opening's length otherwise.</summary>
	[RelayCommand]
	private async Task SkipAhead()
	{
		if (CurrentSegmentEnd() is { } current)
		{
			await SeekAsync(current.End);
			// Not awaited: the message stays up for a while, and the command would be busy (ignoring presses) until it went.
			_ = ShowMessageAsync($"Skipped the {current.Name}", TimeSpan.FromSeconds(2));
			return;
		}

		await SeekBy(OpeningLength.TotalSeconds);
	}

	public (TimeSpan End, string Name)? CurrentSegmentEnd()
	{
		var seconds = Position;
		for (var i = Chapters.Count - 1; i >= 0; i--)
		{
			if (Chapters[i].Start.TotalSeconds <= seconds)
			{
				if (Enum.TryParse<MediaSegmentType>(Chapters[i].Title, out var type))
				{
					// The last chapter ends with the video; while its length isn't known yet, there's no end to skip to.
					if (i + 1 >= Chapters.Count && Duration <= 0)
					{
						return null;
					}

					var end = i + 1 < Chapters.Count ? Chapters[i + 1].Start : TimeSpan.FromSeconds(Duration);
					var name = type switch
					{
						MediaSegmentType.Opening => "opening",
						MediaSegmentType.Ending => "ending",
						_ => "recap",
					};
					return (end, name);
				}

				return null;
			}
		}

		return null;
	}

	public void UpdateViewportSize(double width, double height)
	{
		if (width <= 0 || height <= 0 || (Math.Abs(ViewportWidth - width) < 0.5 && Math.Abs(ViewportHeight - height) < 0.5))
		{
			return;
		}

		ViewportWidth = width;
		ViewportHeight = height;

		// Stretching fills the new size; a fixed ratio is sized to it (Windows' player sizes its element).
		if (AspectRatio != PlayerAspectRatio.Fit)
		{
			ApplyAspectRatio();
		}
	}

	partial void OnAspectRatioChanged(PlayerAspectRatio value)
	{
		ApplyAspectRatio();
	}

	[RelayCommand]
	private void CycleAspectRatio()
	{
		var values = Enum.GetValues<PlayerAspectRatio>();
		var nextIndex = ((int)AspectRatio + 1) % values.Length;
		SetAspectRatio(values[nextIndex]);
	}

	[RelayCommand]
	public void SetAspectRatio(PlayerAspectRatio ratio)
	{
		AspectRatio = ratio;
		_ = ShowMessageAsync($"Aspect ratio: {PlayerAspectRatioText(ratio)}", TimeSpan.FromSeconds(2));
	}

	public static string PlayerAspectRatioText(PlayerAspectRatio ratio) => ratio switch
	{
		PlayerAspectRatio.Fit => "Fit (original)",
		PlayerAspectRatio.Stretch => "Stretch to fill",
		PlayerAspectRatio.Ratio16_9 => "16:9",
		PlayerAspectRatio.Ratio4_3 => "4:3",
		PlayerAspectRatio.Ratio21_9 => "21:9",
		_ => ratio.ToString(),
	};

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

	/// <summary>The watch-together room, while in one.</summary>
	public PlayerRoomViewModel Room { get; } = new();

	/// <summary>The anime's episodes, and moving between them.</summary>
	public PlayerEpisodeListViewModel EpisodeList { get; } = new();

	/// <summary>Plays the next episode: takes up the "Up next" offer if it's showing.</summary>
	[RelayCommand]
	private void NextEpisode()
	{
		if (_next is not null)
		{
			_next.TrySetResult(true);
			return;
		}

		EpisodeList.RequestRelative(1);
	}

	partial void OnVolumeChanged(double value)
	{
		ApplyVolume();
		if (value > 0)
		{
			IsMuted = false;
		}
	}

	partial void OnIsMutedChanged(bool value) => ApplyVolume();

	// libvlc renders into a swap chain, so never stops the display from sleeping itself; a request is harmless for the other.
	partial void OnIsPlayingChanged(bool value)
	{
		if (value)
		{
			(_displayRequest ??= new()).RequestActive();
		}
		else
		{
			_displayRequest?.RequestRelease();
		}
	}

	protected void OnPlaying()
	{
		IsPlaying = true;
		IsBuffering = false;
		ApplyAspectRatio();
		// The volume only applies once there's audio.
		ApplyVolume();
	}

	protected internal void OnTime(TimeSpan time, bool report = true)
	{
		Position = time.TotalSeconds;
		IsBuffering = false;
		UpdateTimeText();
		UpdateSkipButtonState();
		if (report)
		{
			PositionChanged?.Invoke(this, time);
		}
	}

	protected void OnLength(TimeSpan length)
	{
		Duration = length.TotalSeconds;
		UpdateTimeText();
		DurationChanged?.Invoke(this, length);
	}

	protected void OnEnded()
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
		UpdateSkipButtonState();
		UpdateTimeText();
		ShowStatus(null);
	}

	private void UpdateTimeText()
	{
		var duration = TimeSpan.FromSeconds(Duration);
		PositionText = Duration > 0 ? Format(TimeSpan.FromSeconds(Position), duration) : "";
		DurationText = Duration > 0 ? Format(duration, duration) : "";
	}

	public bool HasOpOrEd => Chapters.Any(x => x.Title is "Opening" or "Ending" or "Recap");

	private void UpdateSkipButtonState()
	{
		if (!HasOpOrEd)
		{
			IsSkipButtonVisible = true;
			SkipButtonText = "+85s";
			SkipButtonToolTip = "Skip 85 seconds, an opening's length";
			return;
		}

		var segment = SegmentAt(Position);
		if (segment is MediaSegmentType type)
		{
			IsSkipButtonVisible = true;
			var name = type switch
			{
				MediaSegmentType.Opening => "OP",
				MediaSegmentType.Ending => "ED",
				_ => "recap",
			};
			SkipButtonText = $"Skip {name}";
			SkipButtonToolTip = type switch
			{
				MediaSegmentType.Opening => "Skip opening",
				MediaSegmentType.Ending => "Skip ending",
				_ => "Skip recap",
			};
		}
		else
		{
			IsSkipButtonVisible = false;
		}
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

	// Engines raise their events on their own threads, which mustn't call back into libvlc either.
	protected void OnUi(Action action)
	{
		if (_ui is null)
		{
			Run();
		}
		else
		{
			_ui.TryEnqueue(Run);
		}

		// What throws here would close the app (the app's own handler doesn't see it): it's logged, as that handler does.
		void Run()
		{
			try
			{
				if (!_isClosed)
				{
					action();
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Unhandled exception in the player");
			}
		}
	}

	private static DispatcherQueue? TryGetDispatcherQueue()
	{
		try
		{
			return DispatcherQueue.GetForCurrentThread();
		}
		catch
		{
			return null;
		}
	}
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
