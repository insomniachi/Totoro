using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Windows.Media.Control;

namespace Totoro.Playback;

/// <summary>
/// Marks anime episodes finished in a browser as watched (if it's turned on, see <see cref="PlaybackPreferences"/>), from
/// what browsers tell Windows is playing (its title, and where it is). Only anime on the user's list are updated: a
/// video that merely looks like an episode ("Podcast Episode 12") mustn't add a stranger to it.
/// </summary>
public sealed class BrowserWatchTracker : IDisposable
{
	// Watched this much of the video counts as watched, like the credits do in the player.
	private const double WatchedFraction = 0.85;

	private static readonly string[] s_browsers = ["chrome", "msedge", "firefox", "brave", "opera", "vivaldi"];

	private readonly WatchService _watch;
	private readonly IPreferences _preferences;
	private readonly IFactory<IAnimeMetadataProvider, Guid> _databases;
	private readonly IFactory<IAnimeTracker, Guid> _trackers;
	private readonly ILogger<BrowserWatchTracker> _logger;
	private readonly DispatcherQueueTimer _check;
	private readonly HashSet<(string Title, int Episode)> _done = [];
	private readonly Dictionary<string, AnimeModel?> _found = new(StringComparer.OrdinalIgnoreCase);
	private GlobalSystemMediaTransportControlsSessionManager? _sessions;
	private bool _checking;

	public BrowserWatchTracker(WatchService watch, IPreferences preferences, IFactory<IAnimeMetadataProvider, Guid> databases,
							   IFactory<IAnimeTracker, Guid> trackers, ILogger<BrowserWatchTracker> logger)
	{
		_watch = watch;
		_preferences = preferences;
		_databases = databases;
		_trackers = trackers;
		_logger = logger;
		// Made on the UI thread (at startup), where WatchService changes.
		_check = DispatcherQueue.GetForCurrentThread().CreateTimer();
		_check.Interval = TimeSpan.FromSeconds(10);
		_check.Tick += async (_, _) => await CheckAsync();
		_check.Start();
	}

	private async Task CheckAsync()
	{
		if (_checking || !_preferences.TrackBrowserPlayback)
		{
			return;
		}

		_checking = true;
		try
		{
			_sessions ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
			foreach (var session in _sessions.GetSessions())
			{
				if (!s_browsers.Any(x => session.SourceAppUserModelId.Contains(x, StringComparison.OrdinalIgnoreCase)) || !IsMostlyPlayed(session))
				{
					continue;
				}

				var media = await session.TryGetMediaPropertiesAsync();
				if (BrowserEpisodeTitle.Parse(media.Title, media.Artist) is not { } episode || !_done.Add(episode))
				{
					continue;
				}

				if (await FindAsync(episode.Title) is { } anime && !(anime.TotalEpisodes > 0 && episode.Episode > anime.TotalEpisodes))
				{
					_logger.LogInformation("Episode {Episode} of {Anime} was watched in a browser", episode.Episode, anime.Title);
					await _watch.MarkWatchedAsync(anime, episode.Episode);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't track what's playing in the browser");
		}
		finally
		{
			_checking = false;
		}
	}

	// shortcut: sites that don't say where the video is (no timeline) aren't tracked; count time played if users hit this.
	private static bool IsMostlyPlayed(GlobalSystemMediaTransportControlsSession session)
	{
		var timeline = session.GetTimelineProperties();
		var length = timeline.EndTime - timeline.StartTime;
		if (length <= TimeSpan.Zero)
		{
			return false;
		}

		// Windows is told where it is when that changes (a seek, a pause), not as it plays.
		var position = timeline.Position - timeline.StartTime;
		if (session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
		{
			position += DateTimeOffset.Now - timeline.LastUpdatedTime;
		}

		return position >= length * WatchedFraction;
	}

	private async Task<AnimeModel?> FindAsync(string title)
	{
		if (!_found.TryGetValue(title, out var anime))
		{
			var results = await _databases.CreateDefault().FindAnimeAsync(new AdvancedSearchRequest { Title = title }, _trackers.CreateDefault());
			_found[title] = anime = results.FirstOrDefault(x => x.Tracking is not null);
		}

		return anime;
	}

	public void Dispose() => _check.Stop();
}
