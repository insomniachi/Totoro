using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Windows.Media.Control;

namespace Totoro.Playback;

/// <summary>
/// Marks anime episodes finished in a browser as watched (if it's turned on, see <see cref="PlaybackPreferences"/>), from
/// what browsers tell Windows is playing (its title, and where it is), and shows them in the status bar as they play.
/// Only anime on the user's list are followed: a video that merely looks like an episode ("Podcast Episode 12") mustn't
/// add a stranger to it.
/// </summary>
public sealed class BrowserWatchTracker : IDisposable
{
	// Watched this much of the video counts as watched, like the credits do in the player.
	private const double WatchedFraction = 0.85;

	// Part of the browser's app id, and its name for the status bar.
	private static readonly (string Id, string Name)[] s_browsers =
		[("chrome", "Chrome"), ("msedge", "Edge"), ("firefox", "Firefox"), ("brave", "Brave"), ("opera", "Opera"), ("vivaldi", "Vivaldi")];

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
		// Often enough for the status bar to follow where it is.
		_check.Interval = TimeSpan.FromSeconds(1);
		_check.Tick += async (_, _) => await CheckAsync();
		_check.Start();
	}

	private async Task CheckAsync()
	{
		if (_checking)
		{
			return;
		}

		if (!_preferences.TrackBrowserPlayback)
		{
			_watch.EndBrowserPlayback();
			return;
		}

		_checking = true;
		var shown = false;
		try
		{
			_sessions ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
			foreach (var session in _sessions.GetSessions())
			{
				if (s_browsers.FirstOrDefault(x => session.SourceAppUserModelId.Contains(x.Id, StringComparison.OrdinalIgnoreCase)).Name is not { } browser ||
					GetProgress(session) is not var (position, length))
				{
					continue;
				}

				var media = await session.TryGetMediaPropertiesAsync();
				if (BrowserEpisodeTitle.Parse(media.Title, media.Artist) is not { } episode || await FindAsync(episode.Title) is not { } anime ||
					anime.TotalEpisodes > 0 && episode.Episode > anime.TotalEpisodes)
				{
					continue;
				}

				var countsAt = length * WatchedFraction;
				var marked = false;
				if (position >= countsAt && _done.Add(episode))
				{
					_logger.LogInformation("Episode {Episode} of {Anime} was watched in a browser", episode.Episode, anime.Title);
					await _watch.MarkWatchedAsync(anime, episode.Episode);
					marked = true;
				}

				// shortcut: with episodes in several browsers at once, the status bar shows the first; prefer the playing one if users hit this.
				if (!shown)
				{
					_watch.ShowBrowserPlayback(anime, episode.Episode, browser, position, length, countsAt, marked);
					shown = true;
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't track what's playing in the browser");
			// A failed check (e.g. marking it) isn't the episode ending: the status bar keeps it.
			shown = true;
		}
		finally
		{
			_checking = false;
		}

		if (!shown)
		{
			_watch.EndBrowserPlayback();
		}
	}

	// shortcut: sites that don't say where the video is (no timeline) aren't tracked; count time played if users hit this.
	private static (TimeSpan Position, TimeSpan Length)? GetProgress(GlobalSystemMediaTransportControlsSession session)
	{
		var timeline = session.GetTimelineProperties();
		var length = timeline.EndTime - timeline.StartTime;
		if (length <= TimeSpan.Zero)
		{
			return null;
		}

		// Windows is told where it is when that changes (a seek, a pause), not as it plays.
		var position = timeline.Position - timeline.StartTime;
		if (session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
		{
			position += DateTimeOffset.Now - timeline.LastUpdatedTime;
		}

		return (position < length ? position : length, length);
	}

	private async Task<AnimeModel?> FindAsync(string title)
	{
		if (!_found.TryGetValue(title, out var anime))
		{
			if (_databases.CreateDefault() is not { } database)
			{
				return null;
			}

			var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = title }, _trackers.CreateDefault());
			_found[title] = anime = results.FirstOrDefault(x => x.Tracking is not null);
		}

		return anime;
	}

	public void Dispose() => _check.Stop();
}
