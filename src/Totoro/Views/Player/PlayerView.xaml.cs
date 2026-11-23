using System.ComponentModel;
using System.Numerics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using Totoro.Core.Settings;
using Totoro.UI.Input;
using Totoro.UI.Player;
using Totoro.ViewModels.Player;

namespace Totoro.Views.Player;

/// <summary>
/// Plays in the main window (so a screen share of the app's window shows the episode too), or in a
/// <see cref="PlayerWindow"/> of its own (see <c>PlayInMainWindow</c>), for the players that play in the app (plugins' too).
/// </summary>
internal sealed class PlayerWindowFactory(IPreferences preferences, Func<MainWindow?> mainWindow) : IPlayerWindowFactory
{
	public IPlayerHost Create(PlayerViewModel viewModel, PlayerSettings settings) =>
		preferences.PlayInMainWindow && mainWindow() is { } main
			? new MainWindowPlayerHost(main, new PlayerView(main, viewModel, settings))
			: new WindowPlayerHost(new PlayerWindow(viewModel, settings));
}

internal sealed class WindowPlayerHost : IPlayerHost
{
	private readonly PlayerWindow _window;

	public WindowPlayerHost(PlayerWindow window)
	{
		_window = window;
		window.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
	}

	public event EventHandler? Closed;

	public void Activate() => _window.Activate();

	public void Close() => _window.Close();
}

/// <summary>The player over the whole main window, which comes back as it was when the player closes.</summary>
internal sealed class MainWindowPlayerHost : IPlayerHost
{
	private readonly MainWindow _window;
	private readonly PlayerView _view;
	private bool _isClosed;

	public MainWindowPlayerHost(MainWindow window, PlayerView view)
	{
		_window = window;
		_view = view;
		view.CloseRequested += (_, _) => Close();
		window.ShowPlayer(view, this);
	}

	public event EventHandler? Closed;

	public void Activate() => (_window.BringToFront ?? _window.Activate)();

	public void Close()
	{
		if (_isClosed)
		{
			return;
		}

		_isClosed = true;
		_view.Detach();
		_window.HidePlayer(_view);
		Closed?.Invoke(this, EventArgs.Empty);
	}
}

/// <summary>A window of the player's own (when it doesn't play in the main window), placed over most of the screen.</summary>
internal sealed partial class PlayerWindow : Window
{
	public PlayerWindow(PlayerViewModel viewModel, PlayerSettings settings)
	{
		var view = new PlayerView(this, viewModel, settings);
		Content = view;
		view.Attach();
		PlaceOnScreen();
		view.CloseRequested += (_, _) => Close();
		Activated += OnFirstActivated;
		Closed += (_, _) => view.Detach();

		void OnFirstActivated(object sender, WindowActivatedEventArgs args)
		{
			Activated -= OnFirstActivated;
			view.FocusPlayPause();
		}
	}

	/// <summary>Most of the screen, centred, at 16:9: the video fills the window, the title bar and the controls over it.</summary>
	private void PlaceOnScreen()
	{
		var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
		var width = (int)(area.Width * 0.7);
		AppWindow.ResizeClient(new SizeInt32(width, Math.Min(width * 9 / 16, (int)(area.Height * 0.9))));
		var size = AppWindow.Size;
		AppWindow.Move(new PointInt32(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2));
	}
}

/// <summary>
/// The built-in player: the video with the app's own controls over it, which hide (with the pointer) while it plays and
/// the pointer is still. It fills the window it's in (its own, or the main window), title bar
/// included, and drives that window's title bar, title and full screen.
/// </summary>
public sealed partial class PlayerView : UserControl
{
	private static readonly TimeSpan s_hideControlsAfter = TimeSpan.FromSeconds(3);
	private static readonly TimeSpan s_arrowSeek = TimeSpan.FromSeconds(5);

	private readonly Window _host;
	private readonly DispatcherTimer _hideTimer = new() { Interval = s_hideControlsAfter };
	private CursorHider? _cursorHider;
	private bool _areControlsShown = true;
	// Popups the window already had open as the player loaded (the main window keeps one open): not the player's menus.
	private HashSet<Popup> _windowPopups = [];

	// How far the title bar and the controls slide as they hide.
	private const float ChromeSlide = 24;

	public PlayerView(Window host, PlayerViewModel viewModel, PlayerSettings shortcuts)
	{
		_host = host;
		ViewModel = viewModel;
		Shortcuts = shortcuts;
		InitializeComponent();
		AddVideoView();
		Controls.Attach(viewModel, shortcuts);
	}

	/// <summary>Closing the player (its key), which closes what it's in.</summary>
	public event EventHandler? CloseRequested;

	/// <summary>Takes over its window's title bar, title and full screen: call once the view is the window's (or in it).</summary>
	public void Attach()
	{
		var appWindow = _host.AppWindow;
		// Its own title bar, on Mica like the controls.
		_host.ExtendsContentIntoTitleBar = true;
		_host.SetTitleBar(TitleBarArea);
		appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
		// The taskbar's and Alt+Tab's icon.
		appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Totoro.ico"));
		// White window buttons over the video, in colours of their own (a preferred theme would override them, so the main
		// window's is cleared; it comes back as the player closes), so they can fade with the title bar.
		appWindow.TitleBar.PreferredTheme = TitleBarTheme.Legacy;
		appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF);
		appWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
		appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
		appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
		// The video shows behind the window's own buttons too.
		appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
		appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
		// Launched full screen (a setting): changes after this are applied as they happen, see OnViewModelPropertyChanged.
		if (appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
		{
			// The main window, already full screen (F11): the player follows it.
			ViewModel.IsFullScreen = true;
		}
		else if (ViewModel.IsFullScreen)
		{
			appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
		}

		UpdateTitle();

		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		ViewModel.EpisodeList.PropertyChanged += OnEpisodeListPropertyChanged;
		_hideTimer.Tick += OnHideTimerTick;
		appWindow.Changed += OnAppWindowChanged;
		// In the main window, focus can stay on (or fall back to) the hidden app behind the player: its keys are taken at
		// the window's root, which every key passes through on its way in, so they're the player's wherever focus is.
		if (_host.Content is UIElement root && root != this)
		{
			root.PreviewKeyDown += OnPreviewKeyDown;
		}

		Loaded += OnLoaded;
		SetChromeShown(true);
		ShowControls();
		ViewModel.UpdateViewportSize(VideoArea.ActualWidth, VideoArea.ActualHeight);
	}

	/// <summary>Lets go of its window, as the player closes.</summary>
	public void Detach()
	{
		_hideTimer.Stop();
		_hideTimer.Tick -= OnHideTimerTick;
		_host.AppWindow.Changed -= OnAppWindowChanged;
		if (_host.Content is UIElement root && root != this)
		{
			root.PreviewKeyDown -= OnPreviewKeyDown;
		}

		Loaded -= OnLoaded;
		_cursorHider?.Dispose();
		_cursorHider = null;
		ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
		ViewModel.EpisodeList.PropertyChanged -= OnEpisodeListPropertyChanged;
		Controls.Detach();
	}

	public void FocusPlayPause() => Controls.FocusPlayPause();

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (XamlRoot is { } root)
		{
			_windowPopups = [.. VisualTreeHelper.GetOpenPopupsForXamlRoot(root)];
		}
	}

	public PlayerViewModel ViewModel { get; }

	/// <summary>The player's settings, read for its keys as they're pressed, so a change applies straight away.</summary>
	public PlayerSettings Shortcuts { get; }

	// For x:Bind function bindings.

	public static bool IsBufferingShown(bool isBuffering, bool isStatusVisible) => isBuffering && !isStatusVisible;

	public static bool IsWorking(bool isStatusVisible, bool isError) => isStatusVisible && !isError;

	public static string NoticeText(string person, string text) => $"{person} {text}";

	public static string WindowTitle(string title) => title.Length > 0 ? $"{title} - Totoro" : "Totoro";

	/// <summary>e.g. "Up next · Episode 6".</summary>
	public static string UpNext(string title) => $"Up next · {title}";

	/// <summary>The "Up next" card: 16:9 with a thumbnail (its details over it); as tall as its details without one.</summary>
	public static double NextCardHeight(string image) => image.Length > 0 ? 360 * 9 / 16.0 : double.NaN;

	public static double Opaque(bool shown) => shown ? 1 : 0;

	// The tick grows in as the ring fades: the ring turning into it.
	public static Vector3 TickScale(bool isSaved) => isSaved ? Vector3.One : new(0.4f, 0.4f, 1);

	// Empty after a failed save: it fades out without a word (the failure is said elsewhere).
	public static string SavingText(bool isSaving, bool isSaved) => isSaving ? "Updating your lists…" : isSaved ? "Marked as watched" : "";

	public static Visibility VisibleIfAny(string text) => text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

	// Only the player's own view is made: a plugin engine's may start loading the engine as it's initialized.
	private void AddVideoView()
	{
		UIElement view;
		if (ViewModel.CreateVideoView() is { } own)
		{
			view = own;
		}
		else if (ViewModel is MpvPlayerViewModel mpv)
		{
			// mpv draws in a window of its own inside this one, under the XAML: the window and the video area are see-through
			// over it (they're otherwise painted over it, whatever the windows' order).
			VideoArea.Background = null;
			_host.SystemBackdrop = new WinUIEx.TransparentTintBackdrop();
			_ = mpv.InitializeAsync(Win32Interop.GetWindowFromWindowId(_host.AppWindow.Id));
			return;
		}
		else
		{
			var element = new MediaPlayerElement();
			((MediaFoundationPlayerViewModel)ViewModel).Attach(element);
			view = element;
		}

		VideoArea.Children.Insert(0, view);
	}

	private void OnHideTimerTick(object? sender, object e) => HideControlsIfIdle();

	private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs e)
	{
		if (e.DidPresenterChange)
		{
			ViewModel.IsFullScreen = sender.Presenter.Kind == AppWindowPresenterKind.FullScreen;
		}
	}

	// The episode list opens over the controls (the control shows it): they come back first.
	private void OnEpisodeListPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(PlayerEpisodeListViewModel.IsEpisodeListOpen) && ViewModel.EpisodeList.IsEpisodeListOpen)
		{
			ShowControls();
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(PlayerViewModel.Title):
				UpdateTitle();
				break;
			case nameof(PlayerViewModel.IsPlaying) when !ViewModel.IsPlaying:
				// Paused or over: the controls stay until it plays again.
				ShowControls();
				break;
			case nameof(PlayerViewModel.IsFullScreen):
				var kind = ViewModel.IsFullScreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped;
				if (_host.AppWindow.Presenter.Kind != kind)
				{
					_host.AppWindow.SetPresenter(kind);
				}

				break;
		}
	}

	private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
	{
		// Typing (to the room): the keys are the text's, not the player's.
		if (e.OriginalSource is TextBox)
		{
			return;
		}

		e.Handled = true;
		var keys = Shortcuts;
		switch (e.Key)
		{
			case VirtualKey.Escape when ViewModel.IsFullScreen:
				ViewModel.IsFullScreen = false;
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.PlayPauseKeys, key):
				ViewModel.PlayPauseCommand.Execute(null);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.SeekBackKeys, key):
				_ = ViewModel.SeekAsync(TimeSpan.FromSeconds(ViewModel.Position) - s_arrowSeek);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.SeekForwardKeys, key):
				_ = ViewModel.SeekAsync(TimeSpan.FromSeconds(ViewModel.Position) + s_arrowSeek);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.VolumeUpKeys, key):
				ViewModel.Volume = Math.Min(ViewModel.MaxVolume, ViewModel.Volume + 5);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.VolumeDownKeys, key):
				ViewModel.Volume = Math.Max(0, ViewModel.Volume - 5);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.MuteKeys, key):
				ViewModel.ToggleMuteCommand.Execute(null);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.FullScreenKeys, key):
				ViewModel.ToggleFullScreenCommand.Execute(null);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.EpisodesKeys, key):
				ViewModel.EpisodeList.ToggleEpisodeListCommand.Execute(null);
				break;
			case var key when ViewModel.HasOffer && KeyboardShortcuts.IsPressed(keys.SkipKeys, key):
				ViewModel.AcceptOfferCommand.Execute(null);
				break;
			case var key when ViewModel.IsSkipButtonVisible && KeyboardShortcuts.IsPressed(keys.SkipKeys, key):
				ViewModel.SkipAheadCommand.Execute(null);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.AspectRatioKeys, key):
				ViewModel.CycleAspectRatioCommand.Execute(null);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.NextEpisodeKeys, key):
				ViewModel.NextEpisodeCommand.Execute(null);
				break;
			case var key when KeyboardShortcuts.IsPressed(keys.CloseKeys, key):
				CloseRequested?.Invoke(this, EventArgs.Empty);
				return;
			default:
				e.Handled = false;
				return;
		}

		ShowControls();
	}

	private void OnPointerMoved(object sender, PointerRoutedEventArgs e) => ShowControls();

	private void OnVideoAreaSizeChanged(object sender, SizeChangedEventArgs e)
	{
		ViewModel.UpdateViewportSize(e.NewSize.Width, e.NewSize.Height);
		LiftSubtitles(_areControlsShown);
		PlaceCursorHider();
	}

	private void OnVideoDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => ViewModel.ToggleFullScreenCommand.Execute(null);

	/// <summary>Shows the title bar and the controls (sliding back in), and starts the wait to hide them again.</summary>
	private void ShowControls()
	{
		_hideTimer.Stop();
		_hideTimer.Start();
		if (_areControlsShown)
		{
			return;
		}

		_areControlsShown = true;
		SetCursorHidden(false);
		SetChromeShown(true);
	}

	/// <summary>Slides the title bar and the controls away while it plays and nothing's happening.</summary>
	private void HideControlsIfIdle()
	{
		_hideTimer.Stop();
		// Not while paused, being used, or a menu or the episode list is open.
		if (!ViewModel.IsPlaying || Controls.IsBusy || ViewModel.EpisodeList.IsEpisodeListOpen ||
			XamlRoot is { } root && VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Any(x => !_windowPopups.Contains(x)))
		{
			_hideTimer.Start();
			return;
		}

		_areControlsShown = false;
		SetCursorHidden(true);
		SetChromeShown(false);
	}

	/// <summary>
	/// Fades and slides the title bar (up) and the controls (down) in or out; what's offered over the video follows the
	/// controls down, so it stays near the bottom.
	/// </summary>
	private void SetChromeShown(bool shown)
	{
		var controlsSpace = (float)ControlsSpace;
		Controls.Opacity = shown ? 1 : 0;
		Controls.Translation = shown ? Vector3.Zero : new Vector3(0, ChromeSlide, 0);
		Controls.IsHitTestVisible = shown;
		TitleBarArea.Opacity = shown ? 1 : 0;
		TitleBarArea.Translation = shown ? Vector3.Zero : new Vector3(0, -ChromeSlide, 0);
		TitleLogo.Opacity = TitleBarArea.Opacity;
		TitleLogo.Translation = TitleBarArea.Translation;
		// Down to 24 from the bottom: it sits 12 above the controls.
		Offers.Translation = shown ? Vector3.Zero : new Vector3(0, controlsSpace - 12, 0);
		LiftSubtitles(shown);

		// The window's own buttons (minimise, maximise, close) go too: drawn see-through.
		var titleBar = _host.AppWindow.TitleBar;
		titleBar.ButtonForegroundColor = shown ? Colors.White : Colors.Transparent;
		titleBar.ButtonInactiveForegroundColor = shown ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF) : Colors.Transparent;
	}

	/// <summary>What's offered over the video (the skip button...) sits just above the controls.</summary>
	private void OnControlsSizeChanged(object sender, SizeChangedEventArgs e)
	{
		Offers.Margin = new Thickness(0, 16, 0, ControlsSpace + 12);
		LiftSubtitles(_areControlsShown);
	}

	// The subtitles sit above the controls while they show, else they'd be under them.
	private void LiftSubtitles(bool shown) =>
		ViewModel.SetSubtitleLift(shown && VideoArea.ActualHeight > 0 ? ControlsSpace / VideoArea.ActualHeight : 0);

	// How much of the bottom of the video the floating controls take: their height and the gap under them.
	private double ControlsSpace => Controls.ActualHeight + Controls.Margin.Bottom;

	/// <summary>Hides the pointer over the video (not the title bar, which still drags the window), or shows it again.</summary>
	private void SetCursorHidden(bool hidden)
	{
		if (_cursorHider is { } hider)
		{
			hider.IsHidingCursor = hidden;
		}
	}

	// The overlay that hides the cursor stays over the video (see CursorHider), under the title bar.
	private void PlaceCursorHider()
	{
		if (XamlRoot is not { } root)
		{
			return;
		}

		if (_cursorHider is null)
		{
			_cursorHider = new CursorHider(Win32Interop.GetWindowFromWindowId(_host.AppWindow.Id));
			_cursorHider.Used += (_, _) => ShowControls();
		}

		_cursorHider.Place(0, (int)(TitleBarArea.ActualHeight * root.RasterizationScale));
	}

	private void UpdateTitle() => _host.Title = ViewModel.Title.Length > 0 ? $"{ViewModel.Title} - Totoro" : "Totoro";
}
