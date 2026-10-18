using System.ComponentModel;
using DiscordRPC;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>
/// Shows the episode playing as the user's activity on Discord (if Discord is running and it's turned on, see
/// <see cref="PlaybackPreferences"/>), following <see cref="WatchService"/>; cleared when nothing's playing.
/// </summary>
public sealed class DiscordPresence : IDisposable
{
	// The Totoro application on Discord's developer portal.
	private const string ApplicationId = "997177919052984622";

	// An end time this far off where it was last said is a seek (or a pause, since): said again.
	private static readonly TimeSpan s_drift = TimeSpan.FromSeconds(5);

	private readonly WatchService _watch;
	private readonly IPreferences _preferences;
	private DiscordRpcClient? _client;
	private (string Key, int Episode)? _shown;
	private DateTime _shownEnd;

	public DiscordPresence(WatchService watch, IPreferences preferences)
	{
		_watch = watch;
		_preferences = preferences;
		watch.PropertyChanged += OnWatchChanged;
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
		if (!_preferences.ShowOnDiscord || !_watch.IsActive || _watch.Kind != PlaybackStatusKind.Playing || _watch.Anime is not { } anime)
		{
			Clear();
			return;
		}

		var end = DateTime.UtcNow + (_watch.PlaybackDuration - _watch.PlaybackPosition);
		var known = _watch.PlaybackDuration > TimeSpan.Zero;
		if (_shown == (anime.StoreKey, _watch.CurrentEpisode) && (!known || (end - _shownEnd).Duration() < s_drift))
		{
			return;
		}

		if (_client is null)
		{
			// Keeps trying to connect in the background, sending the presence once Discord is there.
			_client = new DiscordRpcClient(ApplicationId);
			_client.Initialize();
		}

		_shown = (anime.StoreKey, _watch.CurrentEpisode);
		_shownEnd = end;
		_client.SetPresence(new RichPresence
		{
			Type = ActivityType.Watching,
			Details = Fit(anime.Title),
			State = anime.MediaFormat == AnimeMediaFormat.Movie ? null : Fit(_watch.EpisodeLine),
			Assets = new Assets { LargeImageKey = anime.Image, LargeImageText = Fit(anime.EngTitle) },
			Timestamps = known ? new Timestamps(end - _watch.PlaybackDuration, end) : null,
		});
	}

	private void Clear()
	{
		if (_shown is null)
		{
			return;
		}

		_shown = null;
		_client?.ClearPresence();
	}

	// Discord takes at most 128 characters (and at least 2) per line.
	private static string? Fit(string text) => text.Length < 2 ? null : text.Length > 128 ? text[..127] + "…" : text;

	public void Dispose()
	{
		_watch.PropertyChanged -= OnWatchChanged;
		_client?.Dispose();
	}
}
