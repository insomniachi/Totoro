using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Dialogs;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.Core.Module.Media;
using Totoro.Core.Module.Streaming;
using Totoro.Core.Settings;

namespace Totoro.Playback;

public enum PlaybackStatusKind
{
	/// <summary>Working towards playing (finding the show...); can be cancelled.</summary>
	Progress,
	Playing,
	Success,
	Error,
}

/// <summary>An episode to play, as a command parameter.</summary>
public sealed record EpisodeToWatch(AnimeModel Anime, int Number);

/// <summary>Sent (with WeakReferenceMessenger.Default) when an anime's saved playback progress changes.</summary>
/// <param name="AnimeKey">The anime's StoreKey.</param>
public sealed record PlaybackProgressChangedMessage(string AnimeKey);

/// <summary>
/// Plays an anime's episodes: finds the show on the selected anime provider and plays the episode in the selected
/// media player, resuming where it was left off, skipping (or offering to skip) the opening and ending, and marking it
/// watched when it's played to the end, then offering the next one. Reports what it's doing through its properties,
/// for the main window's status bar. One episode at a time.
/// </summary>
public sealed partial class WatchService(
	IFactory<IAnimeProvider, Guid> providers,
	IFactory<IMediaPlayer, Guid> players,
	IFactory<IAnimeTracker, Guid> trackers,
	IEnumerable<IMediaSegmentsProvider> segmentProviders,
	IPlaybackProgressStore progressStore,
	IAnimeSettingsStore animeSettings,
	IEnumerable<IModuleSettings> moduleSettings,
	IDialogService dialogs,
	IPreferences preferences,
	ILogger<WatchService> logger) : ObservableObject
{
	// Progress is only worth keeping (and resuming) past the first moments.
	private static readonly TimeSpan s_minimumProgress = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan s_saveInterval = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan s_nextEpisodeCountdown = TimeSpan.FromSeconds(5);

	private CancellationTokenSource? _cancellation;
	private Action? _action;

	[ObservableProperty] public partial bool IsOpen { get; set; }
	[ObservableProperty] public partial string Title { get; private set; } = "";
	[ObservableProperty] public partial string Message { get; private set; } = "";
	[ObservableProperty] public partial PlaybackStatusKind Kind { get; private set; }

	/// <summary>The status bar's button (Cancel, "Play episode 6"...); empty, never null, when there's none.</summary>
	[ObservableProperty] public partial string ActionText { get; private set; } = "";

	/// <summary>The episode after the last one watched (the first if the anime isn't on the list).</summary>
	public static int NextEpisode(AnimeModel anime) => (anime.Tracking?.WatchedEpisodes ?? 0) + 1;

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
			if (await FindShowAsync(provider, anime, settings, ct) is not { } show)
			{
				IsOpen = false;
				return;
			}

			Show(PlaybackStatusKind.Progress, $"Getting episode {number}…", "Cancel", Cancel);
			if (PickEpisode(await provider.GetEpisodesAsync(show.Id, ct), number) is not { } episode)
			{
				Show(PlaybackStatusKind.Error, $"Episode {number} isn't on {provider.Name} yet.");
				return;
			}

			if ((await provider.GetServersAsync(episode, ct)).FirstOrDefault() is not { } server)
			{
				Show(PlaybackStatusKind.Error, $"{provider.Name} has no video for episode {number}.");
				return;
			}

			// Torrents go to the player as they are: mpv streams them itself, with a webtorrent script.
			var media = new MediaItem(server.Url, $"{anime.Title} - Episode {number}", server.Headers);
			await PlayAsync(player, anime, number, media, settings, ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			// Replaced by another episode, or cancelled: only the latter should clear the status bar.
			if (ReferenceEquals(_cancellation, cancellation))
			{
				IsOpen = false;
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
			player.Play(media, start);
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
			Save();
			WeakReferenceMessenger.Default.Send(new PlaybackProgressChangedMessage(key));
		}

		// Watched to the end, give or take the credits.
		if (duration == TimeSpan.Zero || duration - position > credits)
		{
			IsOpen = false;
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

	/// <summary>Offers the next episode: plays it after a countdown if that's the setting, otherwise on request.</summary>
	private async Task OfferNextAsync(AnimeModel anime, int watched, int next)
	{
		void Offer() => Show(PlaybackStatusKind.Success, $"Marked episode {watched} as watched.", $"Play episode {next}",
							 () => _ = WatchAsync(anime, next));

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
		// The tracker of the database the anime came from; its id means nothing to the others.
		var tracker = (anime.ServiceId is { } serviceId ? trackers.Create(serviceId) : null) ?? trackers.CreateDefault();
		if (tracker is null)
		{
			return;
		}

		var current = anime.Tracking;
		var completed = anime.TotalEpisodes == number;
		anime.Tracking = await tracker.UpdateAsync(anime.Id, new Tracking
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
	/// The provider's show for the anime: the one chosen before, the only exact title match, or one the user picks.
	/// </summary>
	private async Task<ProviderShow?> FindShowAsync(IAnimeProvider provider, AnimeModel anime, AnimeSettings settings, CancellationToken ct)
	{
		if (settings.Shows.TryGetValue(provider.Id, out var known))
		{
			return known;
		}

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

		var results = matches.Values.OrderByDescending(x => x.Score).ToList();
		var exact = results.Where(x => x.Score >= 1).ToList();
		var picked = exact.Count == 1
			? exact[0]
			: await dialogs.ShowAsync<ShowPickerDialogViewModel, ProviderShow>(new ShowPickerRequest(provider, anime.Title, results));

		if (picked is not null)
		{
			settings.Shows[provider.Id] = picked;
			animeSettings.Save(anime, settings);
		}

		return picked;
	}

	/// <summary>The anime's own provider, or the selected one; set to use the anime's own settings for it, if it has any.</summary>
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
	/// The episode with the number, skipping specials like 12.5. Providers sometimes carry on a season's numbering
	/// (a second season starting at 13), so if there's no such number, the episodes are counted from their first.
	/// </summary>
	private static ProviderEpisode? PickEpisode(List<ProviderEpisode> episodes, int number)
	{
		var regular = episodes.Where(x => x.Number == MathF.Floor(x.Number)).ToList();
		if (regular.FirstOrDefault(x => (int)x.Number == number) is { } episode)
		{
			return episode;
		}

		var offset = regular.Count > 0 ? (int)regular.Min(x => x.Number) - 1 : 0;
		return offset > 0 ? regular.FirstOrDefault(x => (int)x.Number == number + offset) : null;
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

	private void Show(PlaybackStatusKind kind, string message, string actionText = "", Action? action = null)
	{
		Kind = kind;
		Message = message;
		ActionText = actionText;
		_action = action;
		IsOpen = true;
	}
}
