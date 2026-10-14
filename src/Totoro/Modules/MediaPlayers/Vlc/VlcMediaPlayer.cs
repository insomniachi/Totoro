using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;
using Totoro.ViewModels.Player;
using Totoro.Views.Player;

namespace Totoro.Modules.MediaPlayers.Vlc;

/// <summary>
/// The built-in player: plays in a window of the app's own (<see cref="PlayerWindow"/>) with libvlc, so the controls
/// are the app's, and skipping the opening and the next episode are offered over the video. The window stays open from
/// one episode to the next; closing it stops playback.
/// </summary>
internal sealed class VlcMediaPlayer(IModuleSettings<Settings> settings, IPreferences preferences, ILogger<VlcMediaPlayer> logger) : IInteractiveMediaPlayer
{
	// The headers libvlc can send itself; any others go through the proxy.
	private static readonly Dictionary<string, string> s_ownHeaders = new(StringComparer.OrdinalIgnoreCase)
	{
		["User-Agent"] = ":http-user-agent",
		["Referer"] = ":http-referrer",
	};

	private readonly HeaderProxy _proxy = new(logger);
	private PlayerWindow? _window;

	public Guid Id => VlcModule.Id;

	public string Name => "the built-in player";

	public event EventHandler<TimeSpan>? DurationChanged;
	public event EventHandler<TimeSpan>? PositionChanged;
	public event EventHandler? PlaybackStopped;
	public event EventHandler<int>? EpisodeRequested;

	public bool IsOpen => _window is not null;

	public bool IsPaused => _window?.ViewModel.IsPaused ?? false;

	/// <summary>
	/// Whether libvlc came with the app (it's copied to the output by its package). Called as watching starts, so it also
	/// starts loading libvlc, while the show and the video are found: the window then opens straight away.
	/// </summary>
	public bool IsAvailable()
	{
		if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64", "libvlc.dll")))
		{
			return false;
		}

		LibVlcLoader.Preload();
		return true;
	}

	public void Play(MediaItem media, TimeSpan startPosition)
	{
		var viewModel = Open();
		_proxy.Clear();

		var uri = media.Uri;
		List<string> options = [];
		var headers = media.Headers ?? new Dictionary<string, string>();
		if (headers.Keys.All(s_ownHeaders.ContainsKey))
		{
			options.AddRange(headers.Select(x => $"{s_ownHeaders[x.Key]}={x.Value}"));
		}
		else
		{
			uri = _proxy.Serve(uri, headers);
		}

		if (settings.Value.AudioLanguage.Length > 0)
		{
			options.Add($":audio-language={settings.Value.AudioLanguage}");
		}

		if (settings.Value.SubtitleLanguage.Length > 0)
		{
			options.Add($":sub-language={settings.Value.SubtitleLanguage}");
		}

		_ = viewModel.PlayAsync(uri, media.Title, options, startPosition, media.IsClip);
	}

	public Task SeekAsync(TimeSpan position) => _window?.ViewModel.SeekAsync(position) ?? Task.CompletedTask;

	public Task ShowMessageAsync(string text, TimeSpan duration) => _window?.ViewModel.ShowMessageAsync(text, duration) ?? Task.CompletedTask;

	public Task SetChaptersAsync(IReadOnlyList<MediaChapter> chapters) => _window?.ViewModel.SetChaptersAsync(chapters) ?? Task.CompletedTask;

	public Task SetPausedAsync(bool paused) => _window?.ViewModel.SetPausedAsync(paused) ?? Task.CompletedTask;

	public IReadOnlyList<MediaTrack> GetTracks() => _window?.ViewModel.GetTracks() ?? [];

	public Task SelectTrackAsync(MediaTrackKind kind, int id) => _window?.ViewModel.SelectTrackAsync(kind, id) ?? Task.CompletedTask;

	public Task<bool> OfferAsync(string text, CancellationToken ct) => _window?.ViewModel.OfferAsync(text, ct) ?? Task.FromResult(false);

	public Task<bool> OfferNextAsync(string title, TimeSpan? countdown, CancellationToken ct)
	{
		return _window?.ViewModel.OfferNextAsync(title, countdown, ct) ?? Task.FromResult(false);
	}

	public void ShowStatus(string? text, bool isError = false) => _window?.ViewModel.ShowStatus(text, isError);

	public void SetEpisodes(IReadOnlyList<PlayerEpisode> episodes) => _window?.ViewModel.SetEpisodes(episodes);

	/// <summary>The window's view model, opening the window if it isn't.</summary>
	private PlayerViewModel Open()
	{
		if (_window is { } open)
		{
			open.Activate();
			return open.ViewModel;
		}

		var viewModel = new PlayerViewModel(preferences.PlayerVolume, preferences.PlayerAspectRatio, logger)
		{
			IsFullScreen = settings.Value.LaunchFullScreen,
		};
		viewModel.DurationChanged += (_, e) => DurationChanged?.Invoke(this, e);
		viewModel.PositionChanged += (_, e) => PositionChanged?.Invoke(this, e);
		viewModel.Ended += (_, _) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
		viewModel.EpisodeRequested += (_, e) => EpisodeRequested?.Invoke(this, e);

		var window = new PlayerWindow(viewModel);
		window.Closed += async (_, _) =>
		{
			_window = null;
			_proxy.Clear();
			preferences.PlayerVolume = (int)viewModel.Volume;
			preferences.PlayerAspectRatio = viewModel.AspectRatio;
			PlaybackStopped?.Invoke(this, EventArgs.Empty);
			await viewModel.CloseAsync();
		};
		_window = window;
		window.Activate();
		return viewModel;
	}
}
