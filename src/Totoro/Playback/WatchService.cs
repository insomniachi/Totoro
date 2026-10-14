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
public sealed record PlaybackStatus(bool IsActive, PlaybackStatusKind Kind, string Title, string Message, string? Position = null);

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
	IDialogService dialogs,
	IPreferences preferences,
	ILogger<WatchService> logger) : ObservableObject
{
	// Progress is only worth keeping (and resuming) past the first moments.
	private static readonly TimeSpan s_minimumProgress = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan s_saveInterval = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan s_nextEpisodeCountdown = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan s_positionInterval = TimeSpan.FromSeconds(1);
	// How long a message (e.g. "Marked episode 5 as watched") stays over the video in the built-in player.
	private static readonly TimeSpan s_playerMessageDuration = TimeSpan.FromSeconds(4);

	private CancellationTokenSource? _cancellation;
	private Action? _action;

	// The player of the episode playing, or of the last one.
	private IMediaPlayer? _player;

	// The built-in player whose episode list is followed (picking an episode plays it), and the anime it lists.
	private IInteractiveMediaPlayer? _listingPlayer;
	private AnimeModel? _listedAnime;

	// Episode details (titles, thumbnails) from ani.zip, by anime: fetched once while watching it.
	private readonly Dictionary<string, List<EpisodeInfo>> _episodeDetails = [];

	/// <summary>Whether the status bar is showing. The user can close it while an episode plays (see <see cref="IsActive"/>).</summary>
	[ObservableProperty] public partial bool IsOpen { get; set; }

	/// <summary>Whether an episode is being got ready or is playing, whether or not the status bar is showing.</summary>
	[ObservableProperty] public partial bool IsActive { get; private set; }
	[ObservableProperty] public partial string Title { get; private set; } = "";
	[ObservableProperty] public partial string Message { get; private set; } = "";
	[ObservableProperty] public partial PlaybackStatusKind Kind { get; private set; }

	/// <summary>The status bar's button (Cancel, "Play episode 6"...); empty, never null, when there's none.</summary>
	[ObservableProperty] public partial string ActionText { get; private set; } = "";

	/// <summary>How far through the episode playback is, from 0 to 1; -1 when nothing's playing or its length isn't known yet.</summary>
	[ObservableProperty] public partial double PlaybackFraction { get; private set; } = -1;

	/// <summary>Where playback is, e.g. "12:34 / 24:01"; empty (never null) when nothing's playing.</summary>
	[ObservableProperty] public partial string PlaybackTime { get; private set; } = "";

	/// <summary>Raised when the status (what the status bar shows) changes.</summary>
	public event EventHandler? StatusChanged;

	/// <summary>What it's up to: the status bar's contents, e.g. for an agent.</summary>
	public PlaybackStatus Status => new(IsActive, Kind, Title, Message, PlaybackTime.Length > 0 ? PlaybackTime : null);

	partial void OnIsActiveChanged(bool value) => StatusChanged?.Invoke(this, EventArgs.Empty);

	/// <summary>The episode after the last one watched (the first if the anime isn't on the list).</summary>
	public static int NextEpisode(AnimeModel anime) => (anime.Tracking?.WatchedEpisodes ?? 0) + 1;

	/// <summary>
	/// Starts playing episode <paramref name="number"/> like <see cref="WatchAsync"/>, but returns once it's playing or
	/// couldn't be played (or, if getting it ready takes longer than <paramref name="timeout"/>, while still at it),
	/// rather than when playback ends: for callers that don't follow the whole playback, e.g. agent tools.
	/// </summary>
	public async Task<PlaybackStatus> StartWatchingAsync(AnimeModel anime, int number, TimeSpan timeout)
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
			var watching = WatchAsync(anime, number);
			await Task.WhenAny(settled.Task, watching, Task.Delay(timeout));
			return Status;
		}
		finally
		{
			StatusChanged -= OnStatusChanged;
		}
	}

	public Task WatchNextAsync(AnimeModel anime) => WatchAsync(anime, NextEpisode(anime));

	/// <summary>Plays episode <paramref name="number"/>, from where it was left off if it wasn't finished.</summary>
	public async Task WatchAsync(AnimeModel anime, int number)
	{
		_cancellation?.Cancel();
		using var cancellation = _cancellation = new CancellationTokenSource();
		var ct = cancellation.Token;

		Title = $"{anime.Title} · Episode {number}";

		try
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
			if (GetProvider(settings) is not { } provider)
			{
				Show(PlaybackStatusKind.Error, "There's no anime provider to watch from.");
				return;
			}

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

			// A torrent is streamed to the player over HTTP, until playback ends.
			await using var stream = server.Kind == VideoServerKind.Torrent ? await StreamTorrentAsync(server, ct) : null;
			var media = new MediaItem(stream?.Url ?? server.Url, $"{anime.Title} - Episode {number}", stream?.Headers ?? server.Headers);
			await PlayAsync(player, anime, number, media, settings, ct);
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
	private async Task PlayAsync(IMediaPlayer player, AnimeModel anime, int number, MediaItem media, AnimeSettings settings, CancellationToken ct)
	{
		var key = anime.StoreKey;
		var credits = TimeSpan.FromMinutes(anime.MediaFormat == AnimeMediaFormat.Movie ? 6 : 2);

		// Resume a little before where it was left, unless that was in the credits.
		var start = progressStore.Get(anime, number) is { } saved && saved.Position > s_minimumProgress && saved.Duration - saved.Position > credits
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

		void Save()
		{
			if (position > s_minimumProgress && duration > TimeSpan.Zero)
			{
				progressStore.Save(anime, number, new EpisodeProgress(position, duration));
			}
		}

		void OnDuration(object? sender, TimeSpan value)
		{
			if (duration == TimeSpan.Zero && value > TimeSpan.Zero)
			{
				// Skip times are matched to the episode's length, now known.
				_ = segments.LoadAsync(segmentProviders.FirstOrDefault(), anime, number, value, ct);
			}

			duration = value;
		}

		void OnPosition(object? sender, TimeSpan value)
		{
			position = value;
			segments.OnPosition(value);
			if (DateTime.Now - lastShown > s_positionInterval)
			{
				lastShown = DateTime.Now;
				var (shownPosition, shownDuration) = (value, duration);
				if (ui is null)
				{
					ShowPosition(shownPosition, shownDuration);
				}
				else
				{
					ui.Post(_ => ShowPosition(shownPosition, shownDuration), null);
				}
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
				_ = ListEpisodesAsync(interactive, anime, number, ct);
			}

			Show(PlaybackStatusKind.Playing, start > TimeSpan.Zero
						 ? $"Resumed at {start:h\\:mm\\:ss} in {player.Name}."
						 : $"Playing in {player.Name}. It's marked as watched if you watch to the end.");
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
			WeakReferenceMessenger.Default.Send(new PlaybackProgressChangedMessage(key));
		}

		// Watched to the end, give or take the credits.
		if (duration == TimeSpan.Zero || duration - position > credits)
		{
			Hide();
			return;
		}

		Show(PlaybackStatusKind.Progress, $"Marking episode {number} as watched…");
		await MarkWatchedAsync(anime, number);
		progressStore.Remove(anime, number);
		WeakReferenceMessenger.Default.Send(new PlaybackProgressChangedMessage(key));

		var next = number + 1;
		if (anime.TotalEpisodes is > 0 and var total && next > total)
		{
			Show(PlaybackStatusKind.Success, $"Marked episode {number} as watched. That was the last one.");
			return;
		}

		await OfferNextAsync(anime, number, next);
	}

	/// <summary>
	/// Offers the next episode: plays it after a countdown if that's the setting, otherwise on request. In the built-in
	/// player it's offered over the video, where the user is, rather than in the status bar.
	/// </summary>
	private async Task OfferNextAsync(AnimeModel anime, int watched, int next)
	{
		void Offer() => Show(PlaybackStatusKind.Success, $"Marked episode {watched} as watched.", $"Play episode {next}",
							 () => _ = WatchAsync(anime, next));

		if (_player is IInteractiveMediaPlayer { IsOpen: true } interactive)
		{
			Show(PlaybackStatusKind.Success, $"Marked episode {watched} as watched.");
			// Cancelled by Cancel, or the next WatchAsync.
			var offer = _cancellation = new CancellationTokenSource();
			if (await interactive.OfferNextAsync($"Episode {next}", preferences.PlayNextAutomatically ? s_nextEpisodeCountdown : null, offer.Token))
			{
				_ = WatchAsync(anime, next);
			}
			else if (!interactive.IsOpen && ReferenceEquals(_cancellation, offer))
			{
				// The player was closed instead: offered here.
				Offer();
			}

			return;
		}

		if (!preferences.PlayNextAutomatically)
		{
			Offer();
			return;
		}

		// Not disposed: Cancel (or the next WatchAsync) may still use it after the countdown.
		var countdown = _cancellation = new CancellationTokenSource();
		try
		{
			for (var left = s_nextEpisodeCountdown; left > TimeSpan.Zero; left -= TimeSpan.FromSeconds(1))
			{
				Show(PlaybackStatusKind.Progress, $"Marked episode {watched} as watched. Playing episode {next} in {left.TotalSeconds:0}…",
					 "Cancel", Cancel);
				await Task.Delay(TimeSpan.FromSeconds(1), countdown.Token);
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

		_ = WatchAsync(anime, next);
	}

	private async Task MarkWatchedAsync(AnimeModel anime, int number)
	{
		var current = anime.Tracking;
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
			return (known, [known]);
		}

		var results = await SearchShowsAsync(provider, anime, ct);
		var exact = results.Where(x => x.Score >= 1).ToList();
		if (exact.Count != 1)
		{
			return (null, results);
		}

		SetShow(anime, provider, exact[0]);
		return (exact[0], results);
	}

	/// <summary>The provider's shows that may be the anime, best match first; a score of 1 is an exact title match.</summary>
	public static async Task<List<ProviderShow>> SearchShowsAsync(IAnimeProvider provider, AnimeModel anime, CancellationToken ct)
	{
		// Any of the anime's titles may be the one the provider uses (SubsPlease uses romaji).
		string[] titles = [.. new[] { anime.Title, anime.RomajiTitle, anime.EngTitle }.Concat(anime.AlternateTitles)
																							.Where(x => !string.IsNullOrWhiteSpace(x))
																							.Distinct(StringComparer.OrdinalIgnoreCase)];
		var matches = new Dictionary<string, ProviderShow>();
		foreach (var title in titles)
		{
			foreach (var show in await provider.SearchAsync(title, ct))
			{
				if (!matches.TryGetValue(show.Id, out var existing) || existing.Score < show.Score)
				{
					matches[show.Id] = show;
				}
			}
		}

		return [.. matches.Values.OrderByDescending(x => x.Score)];
	}

	/// <summary>The provider's show for the anime: as <see cref="FindShowAsync"/> finds it, or else one the user picks.</summary>
	private async Task<ProviderShow?> FindOrPickShowAsync(IAnimeProvider provider, AnimeModel anime, CancellationToken ct)
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

	private IAnimeProvider? GetProvider(AnimeSettings settings)
	{
		var provider = (settings.ProviderId is { } id ? providers.Create(id) : null) ?? providers.CreateDefault();
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
		PickEpisode(episodes, anime, number, numbering, relations);

	internal static ProviderEpisode? PickEpisode(List<ProviderEpisode> episodes, AnimeModel anime, int number, EpisodeNumbering? numbering, IAnimeRelations relations)
	{
		var regular = episodes.Where(x => x.Number == MathF.Floor(x.Number)).ToList();
		ProviderEpisode? Numbered(int n) => regular.FirstOrDefault(x => (int)x.Number == n);

		// Absolute numbering (one show for several seasons): anime-relations says where the episode is.
		if (numbering != EpisodeNumbering.Season && relations.FindAbsoluteEpisode(anime, number) is { } absolute)
		{
			// Worked out: absolute if the provider's numbers go past this season's, or it hasn't this season's number.
			var seasonLength = anime.TotalEpisodes ?? absolute.Rule.DestinationEpisodes.Last;
			var isAbsolute = numbering == EpisodeNumbering.Absolute ||
							 (seasonLength is { } length && regular.Any(x => x.Number > length)) ||
							 Numbered(number) is null;
			if (isAbsolute)
			{
				return Numbered(absolute.Episode);
			}
		}

		var offset = regular.Count > 0 ? (int)regular.Min(x => x.Number) - 1 : 0;
		if (numbering == EpisodeNumbering.Absolute)
		{
			return offset > 0 ? Numbered(number + offset) : null;
		}

		// If this anime is a subsequent season in anime-relations, and the provider has episodes past this season's
		// length (or starting at the absolute episode), the provider is using absolute numbering across seasons.
		// Don't fall back to season numbers (e.g. episode 3 of season 1).
		var ids = RelationIds.Of(anime);
		var destinationRule = relations.GetRules(anime).FirstOrDefault(x => x.Rule.Destination.Matches(ids))?.Rule;
		if (destinationRule is not null && numbering != EpisodeNumbering.Season)
		{
			var seasonLength = anime.TotalEpisodes ?? destinationRule.DestinationEpisodes.Last;
			var isAbsolute = (seasonLength is { } length && regular.Any(x => x.Number > length)) ||
							 regular.Any(x => x.Number >= destinationRule.SourceEpisodes.First);
			if (isAbsolute)
			{
				return null;
			}
		}

		var seasonEpisode = Numbered(number);
		if (seasonEpisode is not null || numbering == EpisodeNumbering.Season)
		{
			return seasonEpisode;
		}

		// No rule: providers that only list a later season's episodes may still use absolute numbering (a second season
		// from 13); count from their first.
		return offset > 0 ? Numbered(number + offset) : null;
	}

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

		if (player.IsAvailable())
		{
			return player;
		}

		Show(PlaybackStatusKind.Error, $"{player.Name} wasn't found. Choose where it's installed.");
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
		IsActive = false;
		IsOpen = false;
		(_player as IInteractiveMediaPlayer)?.ShowStatus(null);
	}

	/// <summary>Shows where playback is (while an episode is playing).</summary>
	private void ShowPosition(TimeSpan position, TimeSpan duration)
	{
		if (Kind != PlaybackStatusKind.Playing || duration <= TimeSpan.Zero)
		{
			return;
		}

		PlaybackFraction = Math.Clamp(position / duration, 0, 1);
		PlaybackTime = $"{FormatTime(position, duration)} / {FormatTime(duration, duration)}";
	}

	/// <summary>m:ss, or h:mm:ss for something an hour or longer (a movie).</summary>
	private static string FormatTime(TimeSpan time, TimeSpan duration)
	{
		return time.ToString(duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
	}

	private void Show(PlaybackStatusKind kind, string message, string actionText = "", Action? action = null)
	{
		if (kind != PlaybackStatusKind.Playing)
		{
			PlaybackFraction = -1;
			PlaybackTime = "";
		}

		Kind = kind;
		Message = message;
		ActionText = actionText;
		_action = action;
		IsActive = kind is PlaybackStatusKind.Progress or PlaybackStatusKind.Playing;
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
				_ = interactive.ShowMessageAsync(message, s_playerMessageDuration);
				break;
			default:
				interactive.ShowStatus(null);
				break;
		}
	}

	/// <summary>
	/// Gives the built-in player the anime's episodes (with their titles and thumbnails, where ani.zip has them) for its
	/// episode list, and plays the one the user picks from it.
	/// </summary>
	private async Task ListEpisodesAsync(IInteractiveMediaPlayer interactive, AnimeModel anime, int current, CancellationToken ct)
	{
		_listedAnime = anime;
		if (!ReferenceEquals(_listingPlayer, interactive))
		{
			_listingPlayer?.EpisodeRequested -= OnEpisodeRequested;
			interactive.EpisodeRequested += OnEpisodeRequested;
			_listingPlayer = interactive;
		}

		try
		{
			if (!_episodeDetails.TryGetValue(anime.StoreKey, out var details))
			{
				details = [.. (await anime.GetEpisodesAsync(ct)).Where(x => !x.IsSpecial)];
				if (details.Count > 0)
				{
					_episodeDetails[anime.StoreKey] = details;
				}
			}

			// The aired ones ani.zip knows of, or else as many as the anime has; the one playing at least.
			var count = Math.Max(current, details.Count > 0 ? details.Max(x => x.EpisodeNumber) : anime.TotalEpisodes ?? current);
			var watched = anime.Tracking?.WatchedEpisodes ?? 0;
			var saved = progressStore.GetAll(anime);
			List<PlayerEpisode> episodes = [];
			for (var number = 1; number <= count; number++)
			{
				var info = details.FirstOrDefault(x => x.EpisodeNumber == number);
				episodes.Add(new PlayerEpisode(
					number,
					info is null ? $"Episode {number}" : EpisodeTitle(info),
					info is null ? "" : EpisodeDetails(info),
					info?.Overview ?? "",
					string.IsNullOrEmpty(info?.Image) ? null : info.Image,
					number <= watched,
					number == current,
					saved.GetValueOrDefault(number)?.Fraction ?? 0));
			}

			if (ReferenceEquals(_listedAnime, anime))
			{
				interactive.SetEpisodes(episodes);
			}
		}
		catch (OperationCanceledException)
		{
			// Another episode was started meanwhile; it lists them.
		}
	}

	private void OnEpisodeRequested(object? sender, int number)
	{
		if (_listedAnime is { } anime)
		{
			_ = WatchAsync(anime, number);
		}
	}

	private static string EpisodeTitle(EpisodeInfo info)
	{
		return new[] { info.Titles.English, info.Titles.Romaji, info.Titles.Japanese }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
			   ?? $"Episode {info.EpisodeNumber}";
	}

	/// <summary>e.g. "3 Oct 2025 · 24 min".</summary>
	private static string EpisodeDetails(EpisodeInfo info)
	{
		List<string> details = [];
		if (info.AirDateUtc is { } aired)
		{
			details.Add(aired.ToLocalTime().ToString("d MMM yyyy"));
		}

		if (info.Runtime > 0)
		{
			details.Add($"{info.Runtime} min");
		}

		return string.Join(" · ", details);
	}

	/// <summary>The player of the episode playing; null when none is.</summary>
	public IMediaPlayer? PlayingPlayer => IsActive && Kind == PlaybackStatusKind.Playing ? _player : null;

	/// <summary>Jumps to <paramref name="position"/> in the episode playing.</summary>
	public Task SeekAsync(TimeSpan position)
	{
		return PlayingPlayer?.SeekAsync(position) ?? throw new InvalidOperationException("Nothing's playing.");
	}

	/// <summary>Pauses or resumes the episode playing (in the built-in player).</summary>
	public Task SetPausedAsync(bool paused) => GetInteractivePlayer().SetPausedAsync(paused);

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
