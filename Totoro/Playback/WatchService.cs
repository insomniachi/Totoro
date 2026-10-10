using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
	/// <summary>Working towards playing (finding the show, connecting...); can be cancelled.</summary>
	Progress,
	Playing,
	Success,
	Error,
}

/// <summary>
/// Plays an anime's next episode (the one after the last watched): finds the show on the selected anime provider,
/// streams the episode to the selected media player, and marks it watched when it's played to the end. Reports what
/// it's doing through its properties, for the main window's status bar. One episode at a time.
/// </summary>
public sealed partial class WatchService(
	IFactory<IAnimeProvider, Guid> providers,
	IFactory<IMediaPlayer, Guid> players,
	IFactory<IAnimeTracker, Guid> trackers,
	IEnumerable<IModuleSettings> moduleSettings,
	IDialogService dialogs,
	IPreferences preferences,
	ILogger<WatchService> logger) : ObservableObject
{
	/// <summary>Preference key of the shows chosen for anime, so they're only looked for once.</summary>
	private const string ShowsKey = "ProviderShows";

	private CancellationTokenSource? _cancellation;

	[ObservableProperty] public partial bool IsOpen { get; set; }
	[ObservableProperty] public partial string Title { get; private set; } = "";
	[ObservableProperty] public partial string Message { get; private set; } = "";
	[ObservableProperty] public partial PlaybackStatusKind Kind { get; private set; }

	/// <summary>Plays the episode after the last one watched (the first if the anime isn't on the list).</summary>
	public async Task WatchNextAsync(AnimeModel anime)
	{
		_cancellation?.Cancel();
		using var cancellation = _cancellation = new CancellationTokenSource();
		var ct = cancellation.Token;

		var number = (anime.Tracking?.WatchedEpisodes ?? 0) + 1;
		Title = $"{anime.Title} · Episode {number}";

		try
		{
			if (anime.TotalEpisodes is > 0 and var total && number > total)
			{
				Show(PlaybackStatusKind.Error, $"You've watched all {total} episodes.");
				return;
			}

			if (await GetPlayerAsync() is not { } player)
			{
				return;
			}

			if (providers.CreateDefault() is not { } provider)
			{
				Show(PlaybackStatusKind.Error, "There's no anime provider to watch from.");
				return;
			}

			Show(PlaybackStatusKind.Progress, $"Finding the show on {provider.Name}…");
			if (await FindShowAsync(provider, anime, ct) is not { } show)
			{
				IsOpen = false;
				return;
			}

			Show(PlaybackStatusKind.Progress, $"Getting episode {number}…");
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
			await PlayAsync(player, anime, number, new MediaItem(server.Url, $"{anime.Title} - Episode {number}", server.Headers), ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			IsOpen = false;
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

	/// <summary>Stops getting the episode ready. Closing the player is what stops playback.</summary>
	[RelayCommand]
	private void Cancel()
	{
		_cancellation?.Cancel();
	}

	/// <summary>Plays the episode and waits for the player to close; marks it watched if it was played to the end.</summary>
	private async Task PlayAsync(IMediaPlayer player, AnimeModel anime, int number, MediaItem media, CancellationToken ct)
	{
		var duration = TimeSpan.Zero;
		var position = TimeSpan.Zero;
		var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		void OnDuration(object? sender, TimeSpan value) => duration = value;
		void OnPosition(object? sender, TimeSpan value) => position = value;
		void OnStopped(object? sender, EventArgs e) => stopped.TrySetResult();

		player.DurationChanged += OnDuration;
		player.PositionChanged += OnPosition;
		player.PlaybackStopped += OnStopped;
		try
		{
			player.Play(media, TimeSpan.Zero);
			Show(PlaybackStatusKind.Playing, $"Playing in {player.Name}. It's marked as watched if you watch to the end.");
			await stopped.Task.WaitAsync(ct);
		}
		finally
		{
			player.DurationChanged -= OnDuration;
			player.PositionChanged -= OnPosition;
			player.PlaybackStopped -= OnStopped;
		}

		// Watched to the end, give or take the credits.
		var credits = TimeSpan.FromMinutes(anime.MediaFormat == AnimeMediaFormat.Movie ? 6 : 2);
		if (duration > TimeSpan.Zero && duration - position <= credits)
		{
			Show(PlaybackStatusKind.Progress, $"Marking episode {number} as watched…");
			await MarkWatchedAsync(anime, number);
			Show(PlaybackStatusKind.Success, $"Marked episode {number} as watched.");
		}
		else
		{
			IsOpen = false;
		}
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
		anime.Tracking = await tracker.Update(anime.Id, new Tracking
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
	private async Task<ProviderShow?> FindShowAsync(IAnimeProvider provider, AnimeModel anime, CancellationToken ct)
	{
		var key = $"{provider.Id}/{anime.ServiceId}/{anime.Id}";
		var chosen = preferences.ReadKey<Dictionary<string, ProviderShow>>(ShowsKey) ?? [];
		if (chosen.TryGetValue(key, out var known))
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
			chosen[key] = picked;
			preferences.SaveKey(ShowsKey, chosen);
		}

		return picked;
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

	private void Show(PlaybackStatusKind kind, string message)
	{
		Kind = kind;
		Message = message;
		IsOpen = true;
	}
}
