using Microsoft.UI.Xaml;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;

namespace Totoro.UI.Player;

/// <summary>
/// A player that plays in a window of the app's own (made by <see cref="IPlayerWindowFactory"/>), so the controls are the
/// app's, and skipping the opening and the next episode are offered over the video. What draws the video is the derived
/// player's (its <see cref="PlayerViewModel"/>), so plugins can bring their own engine. The window stays open from one
/// episode to the next; closing it stops playback.
/// </summary>
public abstract class WindowMediaPlayer(IPlayerWindowFactory windows, IModuleSettings<PlayerSettings> settings, IPreferences preferences, ILogger logger) : IInteractiveMediaPlayer
{
	private Window? _window;
	private PlayerViewModel? _viewModel;

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
	protected PlayerViewModel? ViewModel => _viewModel;

	public bool IsPaused => _viewModel?.IsPaused ?? false;

	public abstract bool IsAvailable();

	public abstract void Play(MediaItem media, TimeSpan startPosition);

	/// <summary>A view model for a new window, playing nothing yet.</summary>
	protected abstract PlayerViewModel CreateViewModel();

	public Task SeekAsync(TimeSpan position) => _viewModel?.SeekAsync(position) ?? Task.CompletedTask;

	public Task ShowMessageAsync(string text, TimeSpan duration) => _viewModel?.ShowMessageAsync(text, duration) ?? Task.CompletedTask;

	public Task SetChaptersAsync(IReadOnlyList<MediaChapter> chapters) => _viewModel?.SetChaptersAsync(chapters) ?? Task.CompletedTask;

	public Task<IReadOnlyList<MediaChapter>> GetChaptersAsync() => Task.FromResult(_viewModel?.GetFileChapters() ?? []);

	public Task SetPausedAsync(bool paused) => _viewModel?.SetPausedAsync(paused) ?? Task.CompletedTask;

	public Task SetSpeedAsync(double speed) => _viewModel?.SetSpeedAsync(speed) ?? Task.CompletedTask;

	public IReadOnlyList<MediaTrack> GetTracks() => _viewModel?.GetTracks() ?? [];

	public Task SelectTrackAsync(MediaTrackKind kind, int id) => _viewModel?.SelectTrackAsync(kind, id) ?? Task.CompletedTask;

	public Task<bool> OfferAsync(string text, CancellationToken ct) => _viewModel?.OfferAsync(text, ct) ?? Task.FromResult(false);

	public Task<bool> OfferNextAsync(string title, TimeSpan? countdown, CancellationToken ct)
	{
		return _viewModel?.OfferNextAsync(title, countdown, ct) ?? Task.FromResult(false);
	}

	public void ShowStatus(string? text, bool isError = false) => _viewModel?.ShowStatus(text, isError);

	public void SetWatchedMark(TimeSpan? countsAt, bool isWatched) => _viewModel?.SetWatchedMark(countsAt, isWatched);

	public void ShowSaving(Task saving) => _ = _viewModel?.ShowSavingAsync(saving);

	public void SetEpisodes(IReadOnlyList<PlayerEpisode> episodes) => _viewModel?.EpisodeList.SetEpisodes(episodes);

	public void SetLogo(string? url) => _viewModel?.Logo = url ?? "";

	public void SetRoomPeople(IReadOnlyList<RoomPerson>? people) => _viewModel?.Room.SetRoomPeople(people);

	public void ShowRoomNotice(string person, string text) => _ = _viewModel?.Room.ShowRoomNoticeAsync(person, text);

	/// <summary>The window's view model, opening the window if it isn't.</summary>
	protected PlayerViewModel Open()
	{
		if (_window is { } open && _viewModel is { } openViewModel)
		{
			open.Activate();
			return openViewModel;
		}

		var viewModel = CreateViewModel();
		viewModel.IsFullScreen = Settings.LaunchFullScreen;
		viewModel.DurationChanged += (_, e) => DurationChanged?.Invoke(this, e);
		viewModel.PositionChanged += (_, e) => PositionChanged?.Invoke(this, e);
		viewModel.Ended += (_, _) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
		viewModel.EpisodeList.EpisodeRequested += (_, e) => EpisodeRequested?.Invoke(this, e);
		viewModel.Room.RoomChatSent += (_, e) => RoomChatSent?.Invoke(this, e);

		var window = windows.Create(viewModel, Settings);
		window.Closed += async (_, _) =>
		{
			_window = null;
			_viewModel = null;
			Proxy.Clear();
			Settings.Volume = (int)viewModel.Volume;
			settings.Save();
			preferences.PlayerAspectRatio = viewModel.AspectRatio;
			PlaybackStopped?.Invoke(this, EventArgs.Empty);
			await viewModel.CloseAsync();
		};
		_window = window;
		_viewModel = viewModel;
		window.Activate();
		return viewModel;
	}
}
