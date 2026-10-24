using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Relations;
using Totoro.Core.Services;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>
/// Downloads the next episode of each anime the user is watching (if it's turned on, see <see cref="DownloadPreferences"/>),
/// from its own provider, while the app's open. The provider's only asked once the episode has aired by the anime's
/// schedule, then less and less often until it has it: providers put episodes up a while after they air.
/// </summary>
public sealed class AutoDownloader : IDisposable
{
	// Often enough to start a download soon after its next check is due; checks that aren't due cost nothing.
	private static readonly TimeSpan s_tick = TimeSpan.FromMinutes(5);

	// The list's watched episodes change as the user watches; the list itself is fetched no more often than this.
	private static readonly TimeSpan s_listAge = TimeSpan.FromMinutes(30);

	private readonly WatchService _watch;
	private readonly DownloadService _downloads;
	private readonly ReleaseScheduleService _schedule;
	private readonly IAnimeSettingsStore _animeSettings;
	private readonly IAnimeRelations _relations;
	private readonly IPreferences _preferences;
	private readonly ILogger<AutoDownloader> _logger;
	private readonly DispatcherQueueTimer _check;

	// This session's: episodes downloaded or queued, and how often the provider didn't have one yet.
	private readonly HashSet<(long Id, int Number)> _queued = [];
	private readonly Dictionary<(long Id, int Number), (int Misses, DateTime NextCheck)> _retries = [];
	private List<AnimeModel>? _watching;
	private DateTime _listAt;
	private bool _checking;

	public AutoDownloader(WatchService watch, DownloadService downloads, ReleaseScheduleService schedule, IAnimeSettingsStore animeSettings,
						  IAnimeRelations relations, IPreferences preferences, ILogger<AutoDownloader> logger)
	{
		_watch = watch;
		_downloads = downloads;
		_schedule = schedule;
		_animeSettings = animeSettings;
		_relations = relations;
		_preferences = preferences;
		_logger = logger;
		// Made on the UI thread (at startup), where downloads are queued and WatchService changes.
		var queue = DispatcherQueue.GetForCurrentThread();
		_check = queue.CreateTimer();
		_check.Interval = s_tick;
		_check.Tick += async (_, _) => await CheckAsync();
		_check.Start();
		// Once the window's up, rather than a tick from now.
		queue.TryEnqueue(DispatcherQueuePriority.Low, async () => await CheckAsync());
	}

	/// <summary>Whether episode <paramref name="number"/> has aired by <paramref name="now"/>, by the anime's schedule.</summary>
	internal static bool IsDue(AnimeModel anime, int number, DateTime now) =>
		anime.AiringSchedule.TryGetValue(number, out var airs) ? airs <= now : EpisodeAvailability.AiredEpisodes(anime, now) >= number;

	/// <summary>How long to wait before asking the provider again, after it didn't have the episode <paramref name="misses"/> times.</summary>
	internal static TimeSpan RetryDelay(int misses) => TimeSpan.FromMinutes(15 * Math.Pow(2, Math.Clamp(misses, 1, 5) - 1));

	private async Task CheckAsync()
	{
		if (_checking || !_preferences.AutoDownloadNext)
		{
			return;
		}

		_checking = true;
		try
		{
			if (_watching is null || DateTime.Now - _listAt >= s_listAge)
			{
				_watching = await _schedule.GetWatchingAsync();
				_listAt = DateTime.Now;
			}

			foreach (var anime in _watching)
			{
				await CheckAsync(anime, DateTime.Now);
			}
		}
		catch (Exception ex)
		{
			// E.g. not signed in, or offline: tried again next tick.
			_logger.LogWarning(ex, "Couldn't check the anime being watched for episodes to download");
		}
		finally
		{
			_checking = false;
		}
	}

	private async Task CheckAsync(AnimeModel anime, DateTime now)
	{
		var number = WatchService.NextEpisode(anime);
		var key = (anime.Id, number);
		if (_queued.Contains(key) || anime.TotalEpisodes is > 0 and var total && number > total || !IsDue(anime, number, now) ||
			_retries.TryGetValue(key, out var retry) && now < retry.NextCheck)
		{
			return;
		}

		if (_downloads.IsDownloaded(anime.Title, number))
		{
			_queued.Add(key);
			return;
		}

		if (_watch.GetProvider(anime) is not { } provider)
		{
			return;
		}

		try
		{
			if (await HasEpisodeAsync(provider, anime, number))
			{
				await _downloads.DownloadEpisodesAsync(anime, number, number, provider);
				_queued.Add(key);
				_retries.Remove(key);
				return;
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't check {Provider} for {Anime} episode {Number}", provider.Name, anime.Title, number);
		}

		var misses = retry.Misses + 1;
		_retries[key] = (misses, now + RetryDelay(misses));
	}

	/// <summary>
	/// Whether the provider has the episode, on the show the anime's matched with (or the only exact title match). No show
	/// picker: this runs unasked, so a show that isn't matched yet isn't downloaded from.
	/// </summary>
	private async Task<bool> HasEpisodeAsync(IAnimeProvider provider, AnimeModel anime, int number)
	{
		if ((await _watch.FindShowAsync(provider, anime, CancellationToken.None)).Show is not { } show)
		{
			return false;
		}

		await _relations.EnsureAvailableAsync(CancellationToken.None);
		var episodes = await provider.GetEpisodesAsync(show.Id, CancellationToken.None);
		return _watch.PickEpisode(episodes, anime, number, _animeSettings.Get(anime).EpisodeNumbering) is not null;
	}

	public void Dispose() => _check.Stop();
}
