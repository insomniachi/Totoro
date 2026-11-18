using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;
using Totoro.Modules.MediaPlayers.Vlc;
using Totoro.ViewModels.Player;
using Totoro.Views.Player;

namespace Totoro.Modules.MediaPlayers;

/// <summary>
/// A player that plays in a window of the app's own (<see cref="PlayerWindow"/>), so the controls are the app's, and
/// skipping the opening and the next episode are offered over the video. What draws the video is the derived player's
/// (its <see cref="PlayerViewModel"/>). The window stays open from one episode to the next; closing it stops playback.
/// </summary>
internal abstract class WindowMediaPlayer(IModuleSettings<PlayerSettings> settings, IPreferences preferences, ILogger logger) : IInteractiveMediaPlayer
{
	private PlayerWindow? _window;

	/// <summary>Serves what's played with HTTP headers the player can't send itself.</summary>
	protected HeaderProxy Proxy { get; } = new(logger);

	protected PlayerSettings Settings => settings.Value;

	protected IPreferences Preferences => preferences;

	protected ILogger Logger => logger;

	public abstract Guid Id { get; }

	public abstract string Name { get; }

	public event EventHandler<TimeSpan>? DurationChanged;
	public event EventHandler<TimeSpan>? PositionChanged;
	public event EventHandler? PlaybackStopped;
	public event EventHandler<int>? EpisodeRequested;
	public event EventHandler<string>? RoomChatSent;

	public bool IsOpen => _window is not null;

	/// <summary>The open window's view model; null while it isn't open.</summary>
	protected PlayerViewModel? ViewModel => _window?.ViewModel;

	public bool IsPaused => _window?.ViewModel.IsPaused ?? false;

	public abstract bool IsAvailable();

	public abstract void Play(MediaItem media, TimeSpan startPosition);

	/// <summary>A view model for a new window, playing nothing yet.</summary>
	protected abstract PlayerViewModel CreateViewModel();

	public Task SeekAsync(TimeSpan position) => _window?.ViewModel.SeekAsync(position) ?? Task.CompletedTask;

	public Task ShowMessageAsync(string text, TimeSpan duration) => _window?.ViewModel.ShowMessageAsync(text, duration) ?? Task.CompletedTask;

	public Task SetChaptersAsync(IReadOnlyList<MediaChapter> chapters) => _window?.ViewModel.SetChaptersAsync(chapters) ?? Task.CompletedTask;

	public Task<IReadOnlyList<MediaChapter>> GetChaptersAsync() => Task.FromResult(_window?.ViewModel.GetFileChapters() ?? []);

	public Task SetPausedAsync(bool paused) => _window?.ViewModel.SetPausedAsync(paused) ?? Task.CompletedTask;

	public Task SetSpeedAsync(double speed) => _window?.ViewModel.SetSpeedAsync(speed) ?? Task.CompletedTask;

	public IReadOnlyList<MediaTrack> GetTracks() => _window?.ViewModel.GetTracks() ?? [];

	public Task SelectTrackAsync(MediaTrackKind kind, int id) => _window?.ViewModel.SelectTrackAsync(kind, id) ?? Task.CompletedTask;

	public Task<bool> OfferAsync(string text, CancellationToken ct) => _window?.ViewModel.OfferAsync(text, ct) ?? Task.FromResult(false);

	public Task<bool> OfferNextAsync(string title, TimeSpan? countdown, CancellationToken ct)
	{
		return _window?.ViewModel.OfferNextAsync(title, countdown, ct) ?? Task.FromResult(false);
	}

	public void ShowStatus(string? text, bool isError = false) => _window?.ViewModel.ShowStatus(text, isError);

	public void SetWatchedMark(TimeSpan? countsAt, bool isWatched) => _window?.ViewModel.SetWatchedMark(countsAt, isWatched);

	public void SetEpisodes(IReadOnlyList<PlayerEpisode> episodes) => _window?.ViewModel.EpisodeList.SetEpisodes(episodes);

	public void SetRoomPeople(IReadOnlyList<RoomPerson>? people) => _window?.ViewModel.Room.SetRoomPeople(people);

	public void ShowRoomNotice(string person, string text) => _ = _window?.ViewModel.Room.ShowRoomNoticeAsync(person, text);

	/// <summary>The window's view model, opening the window if it isn't.</summary>
	protected PlayerViewModel Open()
	{
		if (_window is { } open)
		{
			open.Activate();
			return open.ViewModel;
		}

		var viewModel = CreateViewModel();
		viewModel.IsFullScreen = Settings.LaunchFullScreen;
		viewModel.DurationChanged += (_, e) => DurationChanged?.Invoke(this, e);
		viewModel.PositionChanged += (_, e) => PositionChanged?.Invoke(this, e);
		viewModel.Ended += (_, _) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
		viewModel.EpisodeList.EpisodeRequested += (_, e) => EpisodeRequested?.Invoke(this, e);
		viewModel.Room.RoomChatSent += (_, e) => RoomChatSent?.Invoke(this, e);

		var window = new PlayerWindow(viewModel, Settings);
		window.Closed += async (_, _) =>
		{
			_window = null;
			Proxy.Clear();
			Settings.Volume = (int)viewModel.Volume;
			settings.Save();
			preferences.PlayerAspectRatio = viewModel.AspectRatio;
			PlaybackStopped?.Invoke(this, EventArgs.Empty);
			await viewModel.CloseAsync();
		};
		_window = window;
		window.Activate();
		return viewModel;
	}
}
