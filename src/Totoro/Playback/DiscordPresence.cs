using System.ComponentModel;
using DiscordRPC;
using Microsoft.UI.Dispatching;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>
/// Shows the episode playing as the user's activity on Discord (if Discord is running and it's turned on, see
/// <see cref="PlaybackPreferences"/>), following <see cref="WatchService"/>; cleared when nothing's playing, or the anime is hidden from it (see <see cref="AnimeSettings.HideFromDiscord"/>).
/// </summary>
public sealed class DiscordPresence : IDisposable
{
	// The Totoro application on Discord's developer portal.
	private const string ApplicationId = "997177919052984622";

	// An end time this far off where it was last said is a seek (or a pause, since): said again.
	private static readonly TimeSpan s_drift = TimeSpan.FromSeconds(5);

	// Players only say where playback is while it moves: it's paused once it hasn't for this long.
	private static readonly TimeSpan s_pausedAfter = TimeSpan.FromSeconds(3);

	private readonly WatchService _watch;
	private readonly IPreferences _preferences;
	private readonly IAnimeSettingsStore _animeSettings;
	private DiscordRpcClient? _client;
	private (string Key, int Episode, string Title)? _shown;
	private DateTime _shownEnd;
	private bool _shownPaused;
	private TimeSpan _lastPosition;
	private DateTime _lastMoved;
	private readonly DispatcherQueueTimer _pauseCheck;

	public DiscordPresence(WatchService watch, IPreferences preferences, IAnimeSettingsStore animeSettings)
	{
		_animeSettings = animeSettings;
		_watch = watch;
		_preferences = preferences;
		watch.PropertyChanged += OnWatchChanged;
		// Made on the UI thread (at startup), where WatchService changes.
		_pauseCheck = DispatcherQueue.GetForCurrentThread().CreateTimer();
		_pauseCheck.Interval = TimeSpan.FromSeconds(1);
		_pauseCheck.Tick += (_, _) => Update();
	}

	private void OnWatchChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(WatchService.Kind) or nameof(WatchService.IsActive) or nameof(WatchService.PlaybackFraction) or nameof(WatchService.EpisodeName))
		{
			Update();
		}
	}

	private void Update()
	{
		if (!_preferences.ShowOnDiscord || !_watch.IsActive || _watch.Kind != PlaybackStatusKind.Playing || _watch.Anime is not { } anime ||
			_animeSettings.Get(anime).HideFromDiscord)
		{
			Clear();
			return;
		}

		var now = DateTime.UtcNow;
		if (_watch.PlaybackPosition != _lastPosition || _shown != (anime.StoreKey, _watch.CurrentEpisode, anime.Title))
		{
			_lastPosition = _watch.PlaybackPosition;
			_lastMoved = now;
		}

		var end = now + (_watch.PlaybackDuration - _watch.PlaybackPosition);
		var known = _watch.PlaybackDuration > TimeSpan.Zero;
		var paused = known && now - _lastMoved > s_pausedAfter;
		if (_shown == (anime.StoreKey, _watch.CurrentEpisode, anime.Title) && paused == _shownPaused && (!known || paused || (end - _shownEnd).Duration() < s_drift))
		{
			return;
		}

		if (_client is null)
		{
			// Keeps trying to connect in the background, sending the presence once Discord is there.
			_client = new DiscordRpcClient(ApplicationId);
			_client.Initialize();
		}

		_shown = (anime.StoreKey, _watch.CurrentEpisode, anime.Title);
		_shownEnd = end;
		_shownPaused = paused;
		_pauseCheck.Start();
		var episode = anime.MediaFormat == AnimeMediaFormat.Movie ? "" : _watch.EpisodeLine;
		_client.SetPresence(new RichPresence
		{
			Type = ActivityType.Watching,
			Details = Fit(anime.Title),
			State = Fit(paused ? episode.Length > 0 ? $"{episode} (paused)" : "Paused" : episode),
			Assets = new Assets { LargeImageKey = anime.Image, LargeImageText = Fit(anime.OtherTitle) },
			Timestamps = known && !paused ? new Timestamps(end - _watch.PlaybackDuration, end) : null,
		});
	}

	private void Clear()
	{
		if (_shown is null)
		{
			return;
		}

		_shown = null;
		_pauseCheck.Stop();
		_client?.ClearPresence();
	}

	// Discord takes at most 128 characters (and at least 2) per line.
	private static string? Fit(string text) => text.Length < 2 ? null : text.Length > 128 ? text[..127] + "…" : text;

	public void Dispose()
	{
		_watch.PropertyChanged -= OnWatchChanged;
		_pauseCheck.Stop();
		_client?.Dispose();
	}
}
