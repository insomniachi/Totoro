using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Services;
using Totoro.Core.Relations;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeProviders.Downloads;
using Totoro.ViewModels;

namespace Totoro.Playback;

public enum PlaybackStatusKind
{
	/// <summary>Working towards playing (finding the show...); can be cancelled.</summary>
	Progress,
	Playing,
	Success,
	Error,
}

/// <param name="IsActive">Whether an episode is being got ready or is playing (even with the status bar closed); when it
/// isn't, Kind and Message say how the last one went.</param>
/// <param name="Position">Where playback is, e.g. "12:34 / 24:01", while an episode is playing.</param>
public sealed record PlaybackStatus(
	bool IsActive,
	PlaybackStatusKind Kind,
	string Title,
	string Message,
	string? Position = null,
	[property: Description("Where in the episode playing it counts as watched (it's marked then, as it plays, if most of the way there was played rather than skipped).")]
	string? CountsAsWatchedAt = null,
	[property: Description("Whether the episode playing has been marked watched.")]
	bool IsMarkedWatched = false);

/// <summary>An episode to play, as a command parameter.</summary>
public sealed record EpisodeToWatch(AnimeModel Anime, int Number);

/// <summary>Sent (with WeakReferenceMessenger.Default) when an anime's saved playback progress changes.</summary>
/// <param name="AnimeKey">The anime's StoreKey.</param>
public sealed record PlaybackProgressChangedMessage(string AnimeKey);

/// <summary>
/// Plays an anime's episodes: finds the show on the selected anime provider and plays the episode in the selected
/// media player (streaming it with the selected torrent streamer if it's a torrent), resuming where it was left off,
/// skipping (or offering to skip) the opening and ending, and marking it watched when it's played to the end, then
/// offering the next one. Reports what it's doing through its properties, for the main window's status bar. One episode
/// at a time.
/// </summary>
public sealed partial class WatchService(
	IFactory<IAnimeProvider, Guid> providers,
	IFactory<IMediaPlayer, Guid> players,
	IFactory<ITorrentStreamer, Guid> torrentStreamers,
	AnimeTrackingService tracking,
	IEnumerable<IMediaSegmentsProvider> segmentProviders,
	IPlaybackProgressStore progressStore,
	IAnimeSettingsStore animeSettings,
	IAnimeRelations relations,
	IEnumerable<IModuleSettings> moduleSettings,
	IEnumerable<Descriptor> modules,
	IDialogService dialogs,
	IPreferences preferences,
	ILogger<WatchService> logger) : ObservableObject
{
	// Progress is only worth keeping (and resuming) past the first moments.
	private static readonly TimeSpan s_minimumProgress = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan s_saveInterval = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan s_nextEpisodeCountdown = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan s_positionInterval = TimeSpan.FromSeconds(1);

	// A jump in the playback position longer than this is a seek, not playing.
	private static readonly TimeSpan s_seekStep = TimeSpan.FromSeconds(5);

	// How much of the way to the ending (from where it was started) must have been played for it to count as watched.
	private const double PlayedToCount = 0.5;
	// How long a message (e.g. "Marked episode 5 as watched") stays over the video in the built-in player.
	private static readonly TimeSpan s_playerMessageDuration = TimeSpan.FromSeconds(4);

	private CancellationTokenSource? _cancellation;

	// Whether the episode playing was started with "Binge": then the next one plays after a countdown, whatever
	// the setting, and so on until the countdown's cancelled or another episode is started.
	private bool _binge;

	// Whether the episode playing was started to play the downloads (from a download notification): then they're played
	// first whatever the setting, the next ones too.
	private bool _fromDownloads;

	// Marking the episode playing watched (see MarkWatchedWhilePlayingAsync), once it's started: whether it was, and the message.
	private Task<(bool Marked, string Message)>? _marked;
	private Action? _action;

	// Whether the status bar shows an episode playing in a browser (see ShowBrowserPlayback), not one played here.
	private bool _inBrowser;

	// The player of the episode playing, or of the last one.
	private IMediaPlayer? _player;

	// The built-in player's episode list; made on first use, as it plays through this.
	private PlayerEpisodeList? _episodeList;
	private PlayerEpisodeList EpisodeList => _episodeList ??= new(progressStore, (anime, number) => _ = WatchAsync(anime, number));

	/// <summary>Whether the status bar is showing. The user can close it while an episode plays (see <see cref="IsActive"/>).</summary>
	[ObservableProperty] public partial bool IsOpen { get; set; }

	/// <summary>Whether an episode is being got ready or is playing, whether or not the status bar is showing.</summary>
	[ObservableProperty] public partial bool IsActive { get; private set; }
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CardTitle))]
	public partial string Title { get; private set; } = "";
	[ObservableProperty] public partial string Message { get; private set; } = "";
	[ObservableProperty] public partial PlaybackStatusKind Kind { get; private set; }

	/// <summary>The status bar's button (Cancel, "Play episode 6"...); empty, never null, when there's none.</summary>
	[ObservableProperty] public partial string ActionText { get; private set; } = "";

	/// <summary>How far through the episode playback is, from 0 to 1; -1 when nothing's playing or its length isn't known yet.</summary>
	[ObservableProperty] public partial double PlaybackFraction { get; private set; } = -1;

	/// <summary>How far the countdown to the next episode has run, from 0 to 1; -1 when there's no countdown.</summary>
	[ObservableProperty] public partial double CountdownFraction { get; private set; } = -1;

	/// <summary>Where playback is, e.g. "12:34 / 24:01"; empty (never null) when nothing's playing.</summary>
	[ObservableProperty] public partial string PlaybackTime { get; private set; } = "";

	/// <summary>The anime currently being watched or got ready; null when none is.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CardTitle), nameof(EnglishTitle))]
	public partial AnimeModel? Anime { get; private set; }

	/// <summary>The episode number currently being watched or got ready.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(EpisodeLine))]
	public partial int CurrentEpisode { get; private set; }

	/// <summary>The episode's title if known (e.g. from ani.zip).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(EpisodeLine))]
	public partial string EpisodeName { get; private set; } = "";

	// The status bar's card says the anime, its English title and the episode each once: the title is the anime's alone
	// (Title, with the episode, is for where there's no episode line, e.g. the now-playing button and agents).

	/// <summary>The card's title: the anime's, or (with none) <see cref="Title"/>.</summary>
	public string CardTitle => Anime?.Title ?? Title;

	/// <summary>The anime's English title, under its own; empty when it's the same or there's none.</summary>
	public string EnglishTitle => Anime is { EngTitle: { Length: > 0 } english } anime && !english.Equals(anime.Title, StringComparison.OrdinalIgnoreCase) ? english : "";

	/// <summary>e.g. "5 · The Journey's End", or "Episode 5" without a name (a number alone wouldn't say what it is); empty with no episode.</summary>
	public string EpisodeLine
	{
		get
		{
			if (CurrentEpisode <= 0)
			{
				return EpisodeName;
			}

			if (Anime is { } anime && IsMovie(anime))
			{
				return "";
			}

			var number = $"Episode {CurrentEpisode}";
			return EpisodeName.Length > 0 && !EpisodeName.Equals(number, StringComparison.OrdinalIgnoreCase) ? $"{CurrentEpisode} · {EpisodeName}" : number;
		}
	}

	/// <summary>The image URL to display: episode thumbnail if available, or anime cover image.</summary>
	[ObservableProperty] public partial string ImageUrl { get; private set; } = "";

	/// <summary>The anime's banner image URL, if it has one.</summary>
	[ObservableProperty] public partial string BannerUrl { get; private set; } = "";

	/// <summary>The anime provider being watched from.</summary>
	[ObservableProperty] public partial string? ProviderName { get; private set; }

	/// <summary>The player being used.</summary>
	[ObservableProperty] public partial string? PlayerName { get; private set; }

	/// <summary>Metadata tags, e.g. "TV · Fall 2023 · ★ 8.8 · Madhouse".</summary>
	[ObservableProperty] public partial string Metadata { get; private set; } = "";

	/// <summary>Source and player tags, e.g. "AnimePahe · VLC".</summary>
	[ObservableProperty] public partial string SourceInfo { get; private set; } = "";

	/// <summary>Where playback is, and the episode's length, as of <see cref="PlaybackFraction"/>.</summary>
	public TimeSpan PlaybackPosition { get; private set; }
	public TimeSpan PlaybackDuration { get; private set; }

	/// <summary>Formatted percentage string, e.g. "45%".</summary>
	[ObservableProperty] public partial string PlaybackPercent { get; private set; } = "";

	/// <summary>Whether rich playback or anime details are available to show in the status bar card.</summary>
	[ObservableProperty] public partial bool HasDetails { get; private set; }

	/// <summary>Where in the episode playing it counts as watched, e.g. "21:30"; empty until its length is known.</summary>
	[ObservableProperty] public partial string CountsAsWatchedAt { get; private set; } = "";

	/// <summary>Where it counts as watched as a fraction of the episode, for a notch on the status bar's progress; -1 until known.</summary>
	[ObservableProperty] public partial double CountsAtFraction { get; private set; } = -1;

	/// <summary>Whether the episode playing has been marked watched (it is as playback reaches its ending).</summary>
	[ObservableProperty] public partial bool IsMarkedWatched { get; private set; }

	/// <summary>Raised when the status (what the status bar shows) changes.</summary>
	public event EventHandler? StatusChanged;

	/// <summary>Raised as an episode starts playing from the download folder (so the one after it can be downloaded).</summary>
	public event EventHandler<(AnimeModel Anime, int Number)>? PlayingDownloaded;

	/// <summary>What it's up to: the status bar's contents, e.g. for an agent.</summary>
	public PlaybackStatus Status => new(IsActive, Kind, Title, Message, PlaybackTime.Length > 0 ? PlaybackTime : null,
		CountsAsWatchedAt.Length > 0 ? CountsAsWatchedAt : null, IsMarkedWatched);

	partial void OnIsActiveChanged(bool value) => StatusChanged?.Invoke(this, EventArgs.Empty);
	partial void OnAnimeChanged(AnimeModel? value) => UpdateMetadata();
	partial void OnImageUrlChanged(string value) => UpdateDetails();
	partial void OnProviderNameChanged(string? value) => UpdateMetadata();
	partial void OnPlayerNameChanged(string? value) => UpdateMetadata();

	private void UpdateDetails()
	{
		HasDetails = Anime is not null || !string.IsNullOrEmpty(ImageUrl) || Kind is PlaybackStatusKind.Progress or PlaybackStatusKind.Playing;
	}

	private void UpdateMetadata()
	{
		UpdateDetails();
		if (Anime is null)
		{
			Metadata = "";
			SourceInfo = "";
			return;
		}

		Metadata = PlaybackStatusText.Metadata(Anime);
		SourceInfo = PlaybackStatusText.Source(ProviderName, PlayerName);
	}

	/// <summary>The episode after the last one watched (the first if the anime isn't on the list).</summary>
	public static int NextEpisode(AnimeModel anime) => (anime.Tracking?.WatchedEpisodes ?? 0) + 1;

	/// <summary>
	/// Starts playing episode <paramref name="number"/> like <see cref="WatchAsync"/>, but returns once it's playing or
	/// couldn't be played (or, if getting it ready takes longer than <paramref name="timeout"/>, while still at it),
	/// rather than when playback ends: for callers that don't follow the whole playback, e.g. agent tools.
	/// </summary>
	public Task<PlaybackStatus> StartWatchingAsync(AnimeModel anime, int number, TimeSpan timeout) =>
		StartAsync(() => WatchAsync(anime, number), timeout);

	/// <summary>Starts <see cref="WatchFromAsync"/> like <see cref="StartWatchingAsync"/>.</summary>
	public Task<PlaybackStatus> StartWatchingFromAsync(AnimeModel anime, int number, IAnimeProvider provider, ProviderEpisode episode,
													   VideoServer server, bool track, TimeSpan timeout) =>
		StartAsync(() => WatchFromAsync(anime, number, provider, episode, server, track), timeout);

	private async Task<PlaybackStatus> StartAsync(Func<Task> watch, TimeSpan timeout)
	{
		var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		void OnStatusChanged(object? sender, EventArgs e)
		{
			if (!IsActive || Kind != PlaybackStatusKind.Progress)
			{
				settled.TrySetResult();
			}
		}

		StatusChanged += OnStatusChanged;
		try
		{
			var watching = watch();
			await Task.WhenAny(settled.Task, watching, Task.Delay(timeout));
			return Status;
		}
		finally
		{
			StatusChanged -= OnStatusChanged;
		}
	}

	public async Task WatchNextAsync(AnimeModel anime, bool binge = false) => await WatchAsync(anime, await NextToWatchAsync(anime), binge);

	/// <summary><see cref="NextEpisode"/>, or the first after it that isn't filler if the user skips fillers.</summary>
	public Task<int> NextToWatchAsync(AnimeModel anime) => SkipFillersAsync(anime, NextEpisode(anime));

	/// <summary><paramref name="number"/>, or the first episode after it that isn't filler if the user skips fillers.</summary>
	private async Task<int> SkipFillersAsync(AnimeModel anime, int number)
	{
		if (!preferences.SkipFillers)
		{
			return number;
		}

		var fillers = await anime.GetFillersAsync();
		while (fillers.Contains(number))
		{
			number++;
		}

		return number;
	}

	/// <summary>
	/// Plays episode <paramref name="number"/>, from where it was left off if it wasn't finished. With
	/// <paramref name="binge"/>, the episodes after it play on their own (see <see cref="_binge"/>). With
	/// <paramref name="fromDownloads"/>, from the download folder if it's there, whatever the setting.
	/// </summary>
	public Task WatchAsync(AnimeModel anime, int number, bool binge = false, bool fromDownloads = false) => RunAsync(anime, number, binge, fromDownloads, async ct =>
	{
		if (anime.TotalEpisodes is > 0 and var total && number > total)
		{
			Show(PlaybackStatusKind.Error, $"It only has {total} episodes.");
			return;
		}

		if (await GetPlayerAsync() is not { } player)
		{
			return;
		}

		var settings = animeSettings.Get(anime);
		if (await TryPlayDownloadedAsync(player, anime, number, settings, ct))
		{
			return;
		}

		if (GetProvider(settings) is not { } provider)
		{
			Show(PlaybackStatusKind.Error, "There's no anime provider to watch from: install one from the Plugins page.", "Plugins", () =>
			{
				if (Totoro.UI.Navigation.Navigators.TryGetNavigator("main", out var main))
				{
					_ = main.NavigateToViewModelAsync(typeof(PluginsViewModel));
				}
			});
			return;
		}

		ProviderName = provider.Name;

		Show(PlaybackStatusKind.Progress, $"Finding the show on {provider.Name}…", "Cancel", Cancel);
		if (await FindOrPickShowAsync(provider, anime, ct) is not { } show)
		{
			Hide();
			return;
		}

		Show(PlaybackStatusKind.Progress, $"Getting episode {number}…", "Cancel", Cancel);
		await relations.EnsureAvailableAsync(ct);
		if (PickEpisode(await provider.GetEpisodesAsync(show.Id, ct), anime, number, settings.EpisodeNumbering) is not { } episode)
		{
			// Perhaps the provider uses absolute numbering, and there's no rule for it yet.
			Show(PlaybackStatusKind.Error,
				 $"Episode {number} isn't on {provider.Name} yet. If {provider.Name} uses absolute numbering (on across seasons), set that in its episode numbering.",
				 "Episode numbering…", () => _ = EditNumberingAsync(anime, number));
			return;
		}

		if ((int)episode.Number != number)
		{
			logger.LogInformation("Episode {Episode} of {Anime} is {Provider}'s {ProviderEpisode}", number, anime.Title, provider.Name, episode.Number);
		}

		if ((await provider.GetServersAsync(episode, ct)).FirstOrDefault() is not { } server)
		{
			Show(PlaybackStatusKind.Error, $"{provider.Name} has no video for episode {number}.");
			return;
		}

		await PlayServerAsync(player, anime, number, server, settings, track: true, offerEpisodes: true, ct);
	});

	/// <summary>
	/// Plays episode <paramref name="number"/> from the download folder if it's there (and the user plays downloads first, or asked for them),
	/// whatever the anime's provider; returns whether it did.
	/// </summary>
	private async Task<bool> TryPlayDownloadedAsync(IMediaPlayer player, AnimeModel anime, int number, AnimeSettings settings, CancellationToken ct)
	{
		if (!(_fromDownloads || preferences.PlayDownloadsFirst) || providers.Create(DownloadsModule.Id) is not { } downloads)
		{
			return false;
		}

		// By its ids or exact title only: no show picker for a folder that may not be there.
		if ((await FindShowAsync(downloads, anime, ct)).Show is not { } show)
		{
			return false;
		}

		// Downloads are named by the anime's own episode number, so the provider's numbering doesn't apply.
		if ((await downloads.GetEpisodesAsync(show.Id, ct)).FirstOrDefault(x => x.Number == number) is not { } episode ||
			(await downloads.GetServersAsync(episode, ct)).FirstOrDefault() is not { } server)
		{
			return false;
		}

		ProviderName = downloads.Name;
		PlayingDownloaded?.Invoke(this, (anime, number));
		await PlayServerAsync(player, anime, number, server, settings, track: true, offerEpisodes: true, ct);
		return true;
	}

	/// <summary>
	/// Plays a video the user chose on a provider (from <see cref="IAnimeProvider.GetServersAsync"/>) as the anime's episode
	/// <paramref name="number"/> (see <see cref="GetAnimeEpisodeAsync"/>), leaving the anime's settings (its provider, show)
	/// as they are. With <paramref name="track"/> off it doesn't resume, save progress or mark the episode watched. The next
	/// episode isn't offered either way: it'd be played from the anime's own provider.
	/// </summary>
	public Task WatchFromAsync(AnimeModel anime, int number, IAnimeProvider provider, ProviderEpisode episode, VideoServer server, bool track)
	{
		return RunAsync(anime, number, binge: false, async ct =>
		{
			if (await GetPlayerAsync() is not { } player)
			{
				return;
			}

			ProviderName = provider.Name;
			await PlayServerAsync(player, anime, number, server, animeSettings.Get(anime), track, offerEpisodes: false, ct);
		});
	}

	/// <summary>
	/// The anime's episode that's the provider's <paramref name="episode"/>, numbered as <paramref name="numbering"/> says:
	/// the one playing the anime from that show would find it as (see <see cref="PickEpisode(List{ProviderEpisode}, AnimeModel, int, EpisodeNumbering?)"/>),
	/// so a later season a provider numbers on from the first (29 for its 1) is its own episode. The provider's number when
	/// none of the anime's episodes is that one.
	/// </summary>
	public async Task<int> GetAnimeEpisodeAsync(AnimeModel anime, IReadOnlyList<ProviderEpisode> episodes, ProviderEpisode episode,
												EpisodeNumbering? numbering, CancellationToken ct)
	{
		await relations.EnsureAvailableAsync(ct);
		return ShowMatching.AnimeEpisode([.. episodes], anime, episode, numbering, relations);
	}

	/// <summary>A one-part movie, where "Episode 1" says nothing.</summary>
	private static bool IsMovie(AnimeModel anime) => anime.MediaFormat == AnimeMediaFormat.Movie && anime.TotalEpisodes is null or <= 1;

	private static string PlayingTitle(AnimeModel anime, int number) => IsMovie(anime) ? anime.Title : $"{anime.Title} · Episode {number}";

	/// <summary>Plays the server's video (streaming it first if it's a torrent) until the player closes.</summary>
	private async Task PlayServerAsync(IMediaPlayer player, AnimeModel anime, int number, VideoServer server, AnimeSettings settings, bool track,
									   bool offerEpisodes, CancellationToken ct)
	{
		// A torrent is streamed to the player over HTTP, until playback ends.
		await using var stream = server.Kind == VideoServerKind.Torrent ? await StreamTorrentAsync(server, ct) : null;
		var media = new MediaItem(stream?.Url ?? server.Url, IsMovie(anime) ? anime.Title : $"{anime.Title} - Episode {number}",stream?.Headers ?? server.Headers) { Subtitles = server.Subtitles };
		await PlayAsync(player, anime, number, media, server.Segments, settings, track, offerEpisodes, ct);
	}

	/// <summary>Shows the anime and episode in the status bar, and runs <paramref name="play"/>, saying there how it goes.</summary>
	private Task RunAsync(AnimeModel anime, int number, bool binge, Func<CancellationToken, Task> play) => RunAsync(anime, number, binge, false, play);

	private async Task RunAsync(AnimeModel anime, int number, bool binge, bool fromDownloads, Func<CancellationToken, Task> play)
	{
		_cancellation?.Cancel();
		using var cancellation = _cancellation = new CancellationTokenSource();
		var ct = cancellation.Token;

		_binge = binge;
		_fromDownloads = fromDownloads;
		_inBrowser = false;
		Anime = anime;
		CurrentEpisode = number;
		IsMarkedWatched = false;
		CountsAtFraction = -1;
		ImageUrl = anime.Image;
		BannerUrl = anime.BannerImage ?? "";
		EpisodeName = "";
		ProviderName = null;
		PlayerName = null;
		Title = PlayingTitle(anime, number);

		if (EpisodeList.CachedTitle(anime, number) is { } cachedTitle)
		{
			EpisodeName = cachedTitle;
		}

		try
		{
			await play(ct);
		}
		catch (TorrentStreamException ex)
		{
			logger.LogWarning(ex, "Couldn't stream episode {Episode} of {Anime}", number, anime.Title);
			Show(PlaybackStatusKind.Error, ex.Message);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			// Replaced by another episode, or cancelled: only the latter should clear the status bar.
			if (ReferenceEquals(_cancellation, cancellation))
			{
				Hide();
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Couldn't play episode {Episode} of {Anime}", number, anime.Title);
			Show(PlaybackStatusKind.Error, $"Couldn't play it: {ex.Message}");
		}
		finally
		{
			if (ReferenceEquals(_cancellation, cancellation))
			{
				_cancellation = null;
			}
		}
	}

	/// <summary>Whatever the status bar's button offers.</summary>
	[RelayCommand]
	private void Action()
	{
		_action?.Invoke();
	}

	/// <summary>Stops getting the episode ready, or the countdown to the next one. Closing the player stops playback.</summary>
	private void Cancel()
	{
		_cancellation?.Cancel();
	}

	/// <summary>
	/// Plays the episode and follows it until the player closes: saves the position, handles the opening and ending,
	/// and if it was played to the end, marks it watched and offers the next one.
	/// </summary>
	/// <param name="track">Whether to resume, save progress and mark it watched.</param>
	/// <param name="offerEpisodes">Whether to offer the next episode, and list them in the built-in player, from the anime's own provider.</param>
	/// <param name="knownSegments">The opening and ending the provider knows of; none to find them.</param>
	private async Task PlayAsync(IMediaPlayer player, AnimeModel anime, int number, MediaItem media, IReadOnlyList<MediaSegment> knownSegments, AnimeSettings settings, bool track,
								 bool offerEpisodes, CancellationToken ct)
	{
		var key = anime.StoreKey;
		var credits = TimeSpan.FromMinutes(anime.MediaFormat == AnimeMediaFormat.Movie ? 6 : 2);
		// Only in a binge (see OfferNextAsync), so the player can stay open for it; fillers skipped don't change that.
		media = media with { PlaysNext = offerEpisodes && (preferences.PlayNextAutomatically || _binge) && !IsPastLast(anime, number + 1) };

		// Resume a little before where it was left, unless that was in the credits.
		var start = track && progressStore.Get(anime, number) is { } saved && saved.Position > s_minimumProgress && saved.Duration - saved.Position > credits
			? saved.Position - TimeSpan.FromSeconds(3)
			: TimeSpan.Zero;

		var duration = TimeSpan.Zero;
		var position = start;
		var lastSave = DateTime.MinValue;
		var lastShown = DateTime.MinValue;
		// The player reports from its own thread; the status bar is shown on this one.
		var ui = SynchronizationContext.Current;
		var segments = new SegmentSkipper(player, settings.OpeningSkipMode ?? preferences.OpeningSkipMode, settings.EndingSkipMode ?? preferences.EndingSkipMode, logger);
		var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_marked = null;
		CountsAsWatchedAt = "";
		CountsAtFraction = -1;
		(player as IInteractiveMediaPlayer)?.SetWatchedMark(null, false);

		// It's marked watched as playback reaches the ending (or the credits), while it plays, rather than when the player
		// closes: so it's known before then, and isn't lost if the player is closed early or ends badly. Only once most of
		// the way there has been played, so skipping ahead to the end doesn't count.
		var played = TimeSpan.Zero;
		var marking = false;
		TimeSpan? shownCountsAt = null;

		void Post(Action action)
		{
			if (ui is null)
			{
				action();
			}
			else
			{
				ui.Post(_ => action(), null);
			}
		}

		void Save()
		{
			// Once it's watched, where in the credits it was stopped isn't kept: the next play starts it from the start.
			if (track && !marking && position > s_minimumProgress && duration > TimeSpan.Zero)
			{
				progressStore.Save(anime, number, new EpisodeProgress(position, duration));
			}
		}

		void OnDuration(object? sender, TimeSpan value)
		{
			if (duration == TimeSpan.Zero && value > TimeSpan.Zero)
			{
				// Skip times are matched to the episode's length, now known.
				_ = segments.LoadAsync(knownSegments, segmentProviders.FirstOrDefault(), anime, number, value, ct);
			}

			duration = value;
		}

		void OnPosition(object? sender, TimeSpan value)
		{
			// Played, not skipped: the player reports many times a second, so a jump further than this is a seek.
			var step = value - position;
			if (step > TimeSpan.Zero && step < s_seekStep)
			{
				played += step;
			}

			position = value;
			segments.OnPosition(value);
			if (track && CountsAt(duration, segments.EndingStart, credits) is { } countsAt && !marking)
			{
				if (value >= countsAt && played >= (countsAt - start) * PlayedToCount)
				{
					marking = true;
					Post(() => _ = MarkWatchedWhilePlayingAsync(player, anime, number));
				}
				else if (countsAt != shownCountsAt)
				{
					shownCountsAt = countsAt;
					Post(() => ShowCountsAt(player, countsAt, duration));
				}
			}

			if (DateTime.Now - lastShown > s_positionInterval)
			{
				lastShown = DateTime.Now;
				var (shownPosition, shownDuration) = (value, duration);
				Post(() => ShowPosition(shownPosition, shownDuration));
			}

			if (DateTime.Now - lastSave > s_saveInterval)
			{
				lastSave = DateTime.Now;
				Save();
			}
		}

		void OnStopped(object? sender, EventArgs e) => stopped.TrySetResult();

		player.DurationChanged += OnDuration;
		player.PositionChanged += OnPosition;
		player.PlaybackStopped += OnStopped;
		try
		{
			_player = player;
			player.Play(media, start);
			if (player is IInteractiveMediaPlayer interactive)
			{
				if (offerEpisodes)
				{
					_ = EpisodeList.ListAsync(interactive, anime, number, x => EpisodeName = x, ct);
				}
				else
				{
					// An earlier anime's list would play from its own provider.
					EpisodeList.Forget();
					interactive.SetEpisodes([]);
				}
			}

			// Nothing to say while it plays: the card shows the episode, the player and the progress.
			Show(PlaybackStatusKind.Playing, "");
			await stopped.Task.WaitAsync(ct);
		}
		finally
		{
			player.DurationChanged -= OnDuration;
			player.PositionChanged -= OnPosition;
			player.PlaybackStopped -= OnStopped;
			segments.Stop();
			Save();
			PlaybackFraction = -1;
			PlaybackTime = "";
			PlaybackPercent = "";
			WeakReferenceMessenger.Default.Send(new PlaybackProgressChangedMessage(key));
		}

		// Not watched (stopped before the ending, or skipped to it): nothing more to do.
		if (!marking)
		{
			Hide();
			return;
		}

		// Marked as it played (see MarkWatchedWhilePlayingAsync); waits for that, if it's still going, or starts it if the
		// player stopped just as it reached the ending.
		var (marked, message) = await MarkWatchedWhilePlayingAsync(player, anime, number);
		if (!marked)
		{
			Show(PlaybackStatusKind.Error, message);
			return;
		}

		var next = await SkipFillersAsync(anime, number + 1);
		if (!offerEpisodes)
		{
			// Said with the card's colour and the check, not words.
			Show(PlaybackStatusKind.Success, "");
			return;
		}

		// Not offered past the last episode, or the last aired one: a provider won't have it yet, so nothing's left to show
		// once the player is closed (other players have closed by now; the built-in one stays open at the end).
		if (IsPastLast(anime, next))
		{
			if (player is IInteractiveMediaPlayer { IsOpen: true })
			{
				Show(PlaybackStatusKind.Success, "");
				HideWhenClosed(player);
			}
			else
			{
				Hide();
			}

			return;
		}

		await OfferNextAsync(anime, next);
	}

	/// <summary>
	/// Where playback counts as watched: where the ending starts (if it's known, and in the second half), or else the
	/// credits' usual length before the end. Null until the episode's length is known.
	/// </summary>
	internal static TimeSpan? CountsAt(TimeSpan duration, TimeSpan? endingStart, TimeSpan credits)
	{
		if (duration <= TimeSpan.Zero)
		{
			return null;
		}

		return endingStart is { } ending && ending > duration / 2 && ending < duration
			? ending
			: duration - credits > TimeSpan.Zero ? duration - credits : duration;
	}

	/// <summary>Says, while it plays, where the episode will count as watched: so it's known before closing the player.</summary>
	private void ShowCountsAt(IMediaPlayer player, TimeSpan countsAt, TimeSpan duration)
	{
		CountsAsWatchedAt = PlaybackStatusText.Time(countsAt, duration);
		CountsAtFraction = Math.Clamp(countsAt / duration, 0, 1);
		(player as IInteractiveMediaPlayer)?.SetWatchedMark(countsAt, false);
	}

	/// <summary>
	/// Marks the episode watched as playback reaches its ending, and says so in the status bar and over the video; playback
	/// carries on. The next one is offered when it's over, as before. <see cref="_marked"/> is whether it was, and the
	/// message saying so (or why not).
	/// </summary>
	private Task<(bool Marked, string Message)> MarkWatchedWhilePlayingAsync(IMediaPlayer player, AnimeModel anime, int number)
	{
		// Once per playback.
		if (_marked is { } started)
		{
			return started;
		}

		async Task<(bool, string)> MarkAsync()
		{
			string message;
			bool marked;
			try
			{
				message = await MarkWatchedAsync(anime, number)
					? $"Marked episode {number} as watched."
					: $"Episode {number} was already marked as watched.";
				marked = true;
				progressStore.Remove(anime, number);
				WeakReferenceMessenger.Default.Send(new PlaybackProgressChangedMessage(anime.StoreKey));
				// Shown as a check (in the status bar, and by the length in the built-in player), not said.
				IsMarkedWatched = true;
				(player as IInteractiveMediaPlayer)?.SetWatchedMark(null, true);
			}
			catch (Exception ex)
			{
				// A failure is said, though: it needs explaining.
				logger.LogWarning(ex, "Couldn't mark episode {Episode} of {Anime} as watched", number, anime.Title);
				message = $"Couldn't mark episode {number} as watched: {ex.Message}";
				marked = false;
				if (Kind == PlaybackStatusKind.Playing)
				{
					Show(PlaybackStatusKind.Playing, message);
				}
			}

			// Other players (mpv) have no check to show, so it's said over the video.
			if (player is not IInteractiveMediaPlayer)
			{
				_ = player.ShowMessageAsync(message, s_playerMessageDuration);
			}

			return (marked, message);
		}

		return _marked = MarkAsync();
	}

	/// <summary>
	/// Offers the next episode: plays it after a countdown if that's the setting or it's a binge, otherwise on request. In
	/// the built-in player it's offered over the video, where the user is, rather than in the status bar. Asking for it
	/// after cancelling the countdown ends the binge.
	/// </summary>
	private async Task OfferNextAsync(AnimeModel anime, int next)
	{
		var fromDownloads = _fromDownloads;
		void Offer() => Show(PlaybackStatusKind.Success, "", $"Play episode {next}",
							 () => _ = WatchAsync(anime, next, fromDownloads: fromDownloads));

		var binge = _binge;
		var automatically = preferences.PlayNextAutomatically || binge;
		if (_player is IInteractiveMediaPlayer { IsOpen: true } interactive)
		{
			Show(PlaybackStatusKind.Success, "");
			// Cancelled by Cancel, or the next WatchAsync.
			var offer = _cancellation = new CancellationTokenSource();
			if (await interactive.OfferNextAsync($"Episode {next}", automatically ? s_nextEpisodeCountdown : null, offer.Token))
			{
				_ = WatchAsync(anime, next, binge, fromDownloads);
			}
			else if (!interactive.IsOpen && ReferenceEquals(_cancellation, offer))
			{
				// The player was closed instead: offered here.
				Offer();
			}

			return;
		}

		if (!automatically)
		{
			Offer();
			return;
		}

		// Not disposed: Cancel (or the next WatchAsync) may still use it after the countdown.
		var countdown = _cancellation = new CancellationTokenSource();
		try
		{
			// Ticks often so the bar fills smoothly; the message changes once a second.
			var start = System.Diagnostics.Stopwatch.GetTimestamp();
			var shown = -1.0;
			for (var elapsed = TimeSpan.Zero; elapsed < s_nextEpisodeCountdown; elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start))
			{
				var seconds = Math.Ceiling((s_nextEpisodeCountdown - elapsed).TotalSeconds);
				if (seconds != shown)
				{
					Show(PlaybackStatusKind.Progress, $"Playing episode {next} in {seconds:0}…", "Cancel", Cancel);
					shown = seconds;
				}

				CountdownFraction = elapsed / s_nextEpisodeCountdown;
				await Task.Delay(TimeSpan.FromMilliseconds(50), countdown.Token);
			}
		}
		catch (OperationCanceledException)
		{
			// Cancelled, or another episode was started meanwhile (which then owns the status bar).
			if (ReferenceEquals(_cancellation, countdown))
			{
				Offer();
			}

			return;
		}

		_ = WatchAsync(anime, next, binge, fromDownloads);
	}

	/// <summary>
	/// Marks the anime watched up to the episode. False when it already was: watching an earlier episode again doesn't
	/// take the list back to it, unless the anime is being rewatched (then it's how far the rewatch has got).
	/// </summary>
	internal async Task<bool> MarkWatchedAsync(AnimeModel anime, int number)
	{
		var current = anime.Tracking;
		if (current is { } tracked && tracked.WatchedEpisodes >= number && tracked.Status != ListItemStatus.Rewatching)
		{
			return false;
		}

		var completed = anime.TotalEpisodes == number;
		await tracking.UpdateAsync(anime, new Tracking
		{
			WatchedEpisodes = number,
			Status = completed ? ListItemStatus.Completed
				: current?.Status == ListItemStatus.Rewatching ? ListItemStatus.Rewatching
				: ListItemStatus.Watching,
			StartDate = current?.StartDate ?? DateTime.Today,
			FinishDate = completed ? DateTime.Today : current?.FinishDate,
		});
		if (completed && current?.Score is not > 0)
		{
			// Not awaited: it waits on the user, and playback (or the browser tracker) carries on meanwhile.
			_ = AskScoreAsync(anime);
		}

		return true;
	}

	/// <summary>Asks for a score for an anime just finished without one, in the tracking dialog; saved if given.</summary>
	private async Task AskScoreAsync(AnimeModel anime)
	{
		try
		{
			if (await dialogs.ShowAsync<TrackingDialogViewModel, Tracking>(anime) is { } changes)
			{
				await tracking.UpdateAsync(anime, changes);
			}
		}
		catch (Exception ex)
		{
			// E.g. another dialog was open: only one shows at a time.
			logger.LogWarning(ex, "Couldn't ask for a score for {Anime}", anime.Title);
		}
	}

	/// <summary>
	/// Shows an episode playing in a browser (followed by <see cref="BrowserWatchTracker"/>) in the status bar like one
	/// played here: where it is, where it counts as watched, and once it's marked. Not while one is played here.
	/// </summary>
	/// <param name="marked">Whether it's just been marked watched; it stays marked until another episode is shown.</param>
	internal void ShowBrowserPlayback(AnimeModel anime, int number, string browser, TimeSpan position, TimeSpan duration, TimeSpan countsAt, bool marked)
	{
		if (_cancellation is not null || IsActive && !_inBrowser)
		{
			return;
		}

		if (!_inBrowser || Anime?.StoreKey != anime.StoreKey || CurrentEpisode != number)
		{
			Anime = anime;
			CurrentEpisode = number;
			IsMarkedWatched = false;
			ImageUrl = anime.Image;
			BannerUrl = anime.BannerImage ?? "";
			EpisodeName = "";
			ProviderName = null;
			PlayerName = browser;
			Title = PlayingTitle(anime, number);
			// Nothing here can control the browser's video.
			_player = null;
			Show(PlaybackStatusKind.Playing, "");
			_inBrowser = true;
		}

		IsMarkedWatched |= marked;
		CountsAsWatchedAt = PlaybackStatusText.Time(countsAt, duration);
		CountsAtFraction = Math.Clamp(countsAt / duration, 0, 1);
		ShowPosition(position, duration);
	}

	/// <summary>The browser's episode stopped (see <see cref="ShowBrowserPlayback"/>): says if it was marked watched, as here.</summary>
	internal void EndBrowserPlayback()
	{
		if (!_inBrowser)
		{
			return;
		}

		if (IsMarkedWatched)
		{
			// Said with the card's colour and the check, not words.
			Show(PlaybackStatusKind.Success, "");
			_inBrowser = false;
		}
		else
		{
			Hide();
		}
	}

	/// <summary>
	/// The provider to watch the anime from: its own (see <see cref="AnimeSettings"/>), or the selected one; set to use
	/// the anime's own settings for it, if it has any.
	/// </summary>
	public IAnimeProvider? GetProvider(AnimeModel anime) => GetProvider(animeSettings.Get(anime));

	/// <summary>Matches the anime with a show on the provider: it's watched from that show from now on.</summary>
	public void SetShow(AnimeModel anime, IAnimeProvider provider, ProviderShow show)
	{
		var settings = animeSettings.Get(anime);
		settings.Shows[provider.Id] = show;
		animeSettings.Save(anime, settings);
	}

	/// <summary>
	/// The show to watch the anime from on the provider: the one it's matched with, or else the only exact title match
	/// (which becomes its match). Otherwise no show, and the shows it may be, best match first, to choose from.
	/// </summary>
	public async Task<(ProviderShow? Show, IReadOnlyList<ProviderShow> Candidates)> FindShowAsync(IAnimeProvider provider, AnimeModel anime,
																							   CancellationToken ct)
	{
		if (animeSettings.Get(anime).Shows.TryGetValue(provider.Id, out var known))
		{
			// A download's folder is renamed when its naming changes: one its ids find now replaces the old match.
			if (provider.Id != DownloadsModule.Id || await provider.FindShowAsync(anime.ExternalIds, ct) is not { } byId || byId.Id == known.Id)
			{
				return (known, [known]);
			}
		}

		var results = await ShowMatching.SearchShowsAsync(provider, anime, ct);
		var exact = results.Where(x => x.Score >= 1).ToList();
		if (exact.Count != 1)
		{
			return (null, results);
		}

		SetShow(anime, provider, exact[0]);
		return (exact[0], results);
	}

	/// <summary>The provider's show for the anime: as <see cref="FindShowAsync"/> finds it, or else one the user picks.</summary>
	internal async Task<ProviderShow?> FindOrPickShowAsync(IAnimeProvider provider, AnimeModel anime, CancellationToken ct)
	{
		var (show, candidates) = await FindShowAsync(provider, anime, ct);
		if (show is not null)
		{
			return show;
		}

		var picked = await dialogs.ShowAsync<ShowPickerDialogViewModel, ProviderShow>(new ShowPickerRequest(provider, anime.Title, candidates));
		if (picked is not null)
		{
			SetShow(anime, provider, picked);
		}

		return picked;
	}

	/// <summary>
	/// A copy of the provider's settings for the anime, to change for once (e.g. for a download) and pass to
	/// <see cref="IAnimeProvider.UseSettings"/>: its own, or else the module's. Null if the provider has none.
	/// </summary>
	public DataContainerConfig? CopyProviderSettings(AnimeModel anime, Guid providerId) =>
		moduleSettings.FirstOrDefault(x => x.Descriptor.Id == providerId)?.CreateCopy(animeSettings.Get(anime).ProviderSettings.GetValueOrDefault(providerId));

	private IAnimeProvider? GetProvider(AnimeSettings settings)
	{
		var provider = providers.CreateStreaming(settings.ProviderId);
		if (provider is not null && settings.ProviderSettings.TryGetValue(provider.Id, out var own) &&
			moduleSettings.FirstOrDefault(x => x.Descriptor.Id == provider.Id) is { } module)
		{
			provider.UseSettings(module.CreateCopy(own));
		}

		return provider;
	}

	/// <summary>
	/// The provider's episode for the anime's episode <paramref name="number"/>, skipping specials like 12.5. Providers may
	/// use absolute numbering, carrying on across seasons (one show for both seasons, the second from 13): then
	/// anime-relations (and the user's rules) say where the episode is. <paramref name="numbering"/> is the anime's
	/// setting: by season, absolute, or (null) worked out from the provider's episode numbers.
	/// </summary>
	internal ProviderEpisode? PickEpisode(List<ProviderEpisode> episodes, AnimeModel anime, int number, EpisodeNumbering? numbering) =>
		ShowMatching.PickEpisode(episodes, anime, number, numbering, relations);

	/// <summary>Edits the anime's settings (its episode numbering, a rule for it), then plays the episode again if saved.</summary>
	private async Task EditNumberingAsync(AnimeModel anime, int number)
	{
		if (await dialogs.ShowAsync<AnimeSettingsDialogViewModel, bool>(anime))
		{
			await WatchAsync(anime, number);
		}
	}

	/// <summary>
	/// Streams the torrent's episode with the selected torrent streamer, showing what it's doing until it's ready. Null
	/// if there's no streamer, so the player gets the torrent as it is (mpv can stream it with a webtorrent script).
	/// </summary>
	private async Task<TorrentStream?> StreamTorrentAsync(VideoServer server, CancellationToken ct)
	{
		if (torrentStreamers.CreateDefault() is not { } streamer || !streamer.IsAvailable())
		{
			logger.LogWarning("No torrent streamer is available; the player gets the torrent itself");
			return null;
		}

		// Progress<T> reports on the UI thread after the fact; ignore reports that arrive once the stream is ready, so they
		// don't replace the "Playing" status.
		var ready = false;
		var progress = new Progress<string>(message =>
		{
			if (!ready)
			{
				Show(PlaybackStatusKind.Progress, message, "Cancel", Cancel);
			}
		});

		Show(PlaybackStatusKind.Progress, $"Starting {streamer.Name}…", "Cancel", Cancel);
		var stream = await streamer.StreamAsync(server.Url, TorrentFile.LargestVideo, progress, ct);
		ready = true;
		return stream;
	}

	/// <summary>The selected media player, if it's set up; otherwise asks for its settings.</summary>
	private async Task<IMediaPlayer?> GetPlayerAsync()
	{
		if (players.CreateDefault() is not { } player)
		{
			Show(PlaybackStatusKind.Error, "There's no media player to play with.");
			return null;
		}

		// As its module is named (e.g. "Built-in player"): the player's own name reads in a sentence ("the built-in player").
		PlayerName = modules.FirstOrDefault(x => x.Id == player.Id)?.Name ?? player.Name;
		if (player.IsAvailable())
		{
			return player;
		}

		Show(PlaybackStatusKind.Error, $"{player.Name} wasn't found. Download it, or choose where it's installed.");
		if (moduleSettings.FirstOrDefault(x => x.Descriptor.Id == player.Id) is { } settings && await dialogs.EditSettingsAsync(settings) &&
			player.IsAvailable())
		{
			return player;
		}

		return null;
	}

	/// <summary>Shows the status bar again after the user closed it, e.g. to see how playing is going.</summary>
	[RelayCommand]
	private void ShowStatus() => IsOpen = true;

	/// <summary>Nothing more to show: closes the status bar.</summary>
	private void Hide()
	{
		_inBrowser = false;
		IsActive = false;
		IsOpen = false;
		Anime = null;
		CurrentEpisode = 0;
		IsMarkedWatched = false;
		CountsAtFraction = -1;
		ImageUrl = "";
		BannerUrl = "";
		EpisodeName = "";
		ProviderName = null;
		PlayerName = null;
		Metadata = "";
		SourceInfo = "";
		PlaybackPercent = "";
		HasDetails = false;
		(_player as IInteractiveMediaPlayer)?.ShowStatus(null);
	}

	/// <summary>Whether episode <paramref name="number"/> is past the last one, or the last one aired: no provider has it yet.</summary>
	private static bool IsPastLast(AnimeModel anime, int number) =>
		anime.TotalEpisodes is > 0 and var total && number > total ||
		EpisodeAvailability.AiredEpisodes(anime, DateTime.Now) is > 0 and var aired && number > aired;

	/// <summary>Closes the status bar when the player's window is closed, unless another episode has been started meanwhile.</summary>
	private void HideWhenClosed(IMediaPlayer player)
	{
		void OnStopped(object? sender, EventArgs e)
		{
			// Once: a later stop belongs to whatever plays next.
			player.PlaybackStopped -= OnStopped;
			if (player is IInteractiveMediaPlayer { IsOpen: false } && ReferenceEquals(_player, player) &&
				Kind == PlaybackStatusKind.Success)
			{
				Hide();
			}
		}

		player.PlaybackStopped += OnStopped;
	}

	/// <summary>Shows where playback is (while an episode is playing).</summary>
	private void ShowPosition(TimeSpan position, TimeSpan duration)
	{
		if (Kind != PlaybackStatusKind.Playing || duration <= TimeSpan.Zero)
		{
			return;
		}

		(PlaybackPosition, PlaybackDuration) = (position, duration);
		PlaybackFraction = Math.Clamp(position / duration, 0, 1);
		PlaybackTime = $"{PlaybackStatusText.Time(position, duration)} / {PlaybackStatusText.Time(duration, duration)}";
		PlaybackPercent = $"{(int)Math.Round(PlaybackFraction * 100)}%";
	}

	private void Show(PlaybackStatusKind kind, string message, string actionText = "", Action? action = null)
	{
		CountdownFraction = -1;
		if (kind != PlaybackStatusKind.Playing)
		{
			PlaybackFraction = -1;
			PlaybackTime = "";
			PlaybackPercent = "";
		}

		Kind = kind;
		Message = message;
		ActionText = actionText;
		_action = action;
		IsActive = kind is PlaybackStatusKind.Progress or PlaybackStatusKind.Playing;
		UpdateDetails();
		IsOpen = true;
		StatusChanged?.Invoke(this, EventArgs.Empty);
		ShowInPlayer(kind, message);
	}

	/// <summary>
	/// Shows the status in the built-in player too, while it's open (e.g. getting the next episode ready, after the last
	/// one ended in it): the user is watching there, not the main window.
	/// </summary>
	private void ShowInPlayer(PlaybackStatusKind kind, string message)
	{
		if (_player is not IInteractiveMediaPlayer { IsOpen: true } interactive)
		{
			return;
		}

		switch (kind)
		{
			case PlaybackStatusKind.Progress:
				interactive.ShowStatus(message);
				break;
			case PlaybackStatusKind.Error:
				interactive.ShowStatus(message, isError: true);
				break;
			case PlaybackStatusKind.Success:
				interactive.ShowStatus(null);
				if (message.Length > 0)
				{
					_ = interactive.ShowMessageAsync(message, s_playerMessageDuration);
				}

				break;
			default:
				interactive.ShowStatus(null);
				break;
		}
	}

	/// <summary>The player of the episode playing; null when none is.</summary>
	public IMediaPlayer? PlayingPlayer => IsActive && Kind == PlaybackStatusKind.Playing ? _player : null;

	/// <summary>Jumps to <paramref name="position"/> in the episode playing.</summary>
	public Task SeekAsync(TimeSpan position)
	{
		return PlayingPlayer?.SeekAsync(position) ?? throw new InvalidOperationException("Nothing's playing.");
	}

	/// <summary>Pauses or resumes the episode playing.</summary>
	public Task SetPausedAsync(bool paused) => PlayingPlayer?.SetPausedAsync(paused) ?? throw new InvalidOperationException("Nothing's playing.");

	/// <summary>The audio and subtitle tracks of the episode playing (in the built-in player).</summary>
	public IReadOnlyList<MediaTrack> GetTracks() => GetInteractivePlayer().GetTracks();

	/// <summary>Switches the audio or subtitles of the episode playing (in the built-in player).</summary>
	public Task SelectTrackAsync(MediaTrackKind kind, int id) => GetInteractivePlayer().SelectTrackAsync(kind, id);

	private IInteractiveMediaPlayer GetInteractivePlayer()
	{
		return PlayingPlayer switch
		{
			null => throw new InvalidOperationException("Nothing's playing."),
			IInteractiveMediaPlayer interactive => interactive,
			var other => throw new InvalidOperationException($"It's playing in {other.Name}, which the app can't control; only the built-in player."),
		};
	}
}
