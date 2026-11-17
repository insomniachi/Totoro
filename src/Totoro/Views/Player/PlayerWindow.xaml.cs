using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using LibVLCSharp.Platforms.Windows;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using Totoro.Core.Modules.Media;
using VlcSettings = Totoro.Modules.MediaPlayers.Vlc.Settings;
using Totoro.UI.Input;
using Totoro.ViewModels.Player;

namespace Totoro.Views.Player;

/// <summary>
/// The built-in player's window: the video with the app's own controls over it, which hide (with the pointer) while
/// it plays and the pointer is still.
/// </summary>
public sealed partial class PlayerWindow : Window
{
	private static readonly TimeSpan s_hideControlsAfter = TimeSpan.FromSeconds(3);
	private static readonly TimeSpan s_arrowSeek = TimeSpan.FromSeconds(5);

	private readonly DispatcherTimer _hideTimer = new() { Interval = s_hideControlsAfter };
	private bool _isCursorHidden;
	private bool _areControlsShown = true;

	// How far the title bar and the controls slide as they hide.
	private const float ChromeSlide = 24;

	public PlayerWindow(PlayerViewModel viewModel, VlcSettings shortcuts)
	{
		ViewModel = viewModel;
		Shortcuts = shortcuts;
		InitializeComponent();
		Controls.Attach(viewModel, shortcuts);

		// Its own title bar, on Mica like the controls.
		ExtendsContentIntoTitleBar = true;
		SetTitleBar(TitleBarArea);
		AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
		// The taskbar's and Alt+Tab's icon.
		AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Totoro.ico"));
		// White window buttons over the video, in colours of their own (a preferred theme would override them), so they can
		// fade with the title bar.
		AppWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF);
		AppWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);
		AppWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
		AppWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
		// The video shows behind the window's own buttons too.
		AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
		AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
		PlaceOnScreen();
		// Launched full screen (a setting): changes after this are applied as they happen, see OnViewModelPropertyChanged.
		if (ViewModel.IsFullScreen)
		{
			AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
		}

		UpdateTitle();

		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		ViewModel.EpisodeList.PropertyChanged += OnEpisodeListPropertyChanged;
		_hideTimer.Tick += (_, _) => HideControlsIfIdle();
		AppWindow.Changed += (_, e) =>
		{
			if (e.DidPresenterChange)
			{
				ViewModel.IsFullScreen = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
			}
		};

		Activated += OnFirstActivated;
		Closed += (_, _) =>
		{
			_hideTimer.Stop();
			SetCursorHidden(false);
			ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
			ViewModel.EpisodeList.PropertyChanged -= OnEpisodeListPropertyChanged;
			Controls.Detach();
		};
		SetChromeShown(true);
		ShowControls();
		ViewModel.UpdateViewportSize(VideoArea.ActualWidth, VideoArea.ActualHeight);
	}

	public PlayerViewModel ViewModel { get; }

	/// <summary>The player's settings, read for its keys as they're pressed, so a change applies straight away.</summary>
	public VlcSettings Shortcuts { get; }

	// For x:Bind function bindings.

	public static bool IsBufferingShown(bool isBuffering, bool isStatusVisible) => isBuffering && !isStatusVisible;

	public static bool IsWorking(bool isStatusVisible, bool isError) => isStatusVisible && !isError;

	public static string NoticeText(string person, string text) => $"{person} {text}";

	public static string WindowTitle(string title) => title.Length > 0 ? $"{title} - Totoro" : "Totoro";

	/// <summary>e.g. "Up next · Episode 6".</summary>
	public static string UpNext(string title) => $"Up next · {title}";

	/// <summary>The "Up next" card: 16:9 with a thumbnail (its details over it); as tall as its details without one.</summary>
	public static double NextCardHeight(string image) => image.Length > 0 ? 360 * 9 / 16.0 : double.NaN;

	public static Visibility VisibleIfAny(string text) => text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

	private void OnVideoInitialized(object? sender, InitializedEventArgs e) => _ = ViewModel.InitializeAsync(e.SwapChainOptions);

	private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
	{
		Activated -= OnFirstActivated;
		Controls.FocusPlayPause();
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
				if (AppWindow.Presenter.Kind != kind)
				{
					AppWindow.SetPresenter(kind);
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
				ViewModel.Volume = Math.Min(100, ViewModel.Volume + 5);
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
			default:
				e.Handled = false;
				return;
		}

		ShowControls();
	}

	private void OnPointerMoved(object sender, PointerRoutedEventArgs e) => ShowControls();

	private void OnVideoAreaSizeChanged(object sender, SizeChangedEventArgs e) =>
		ViewModel.UpdateViewportSize(e.NewSize.Width, e.NewSize.Height);

	private void OnVideoTapped(object sender, TappedRoutedEventArgs e) => ViewModel.PlayPauseCommand.Execute(null);

	private void OnVideoDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		// The double click's first click played or paused it: undo that.
		ViewModel.PlayPauseCommand.Execute(null);
		ViewModel.ToggleFullScreenCommand.Execute(null);
	}

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
			Content.XamlRoot is { } root && VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Count > 0)
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
		// Down to 24 from the bottom: it sits 12 above the controls.
		Offers.Translation = shown ? Vector3.Zero : new Vector3(0, controlsSpace - 12, 0);

		// The window's own buttons (minimise, maximise, close) go too: drawn see-through.
		var titleBar = AppWindow.TitleBar;
		titleBar.ButtonForegroundColor = shown ? Colors.White : Colors.Transparent;
		titleBar.ButtonInactiveForegroundColor = shown ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF) : Colors.Transparent;
	}

	/// <summary>What's offered over the video (the skip button...) sits just above the controls.</summary>
	private void OnControlsSizeChanged(object sender, SizeChangedEventArgs e)
	{
		Offers.Margin = new Thickness(0, 16, 0, ControlsSpace + 12);
	}

	// How much of the bottom of the video the floating controls take: their height and the gap under them.
	private double ControlsSpace => Controls.ActualHeight + Controls.Margin.Bottom;

	private void SetCursorHidden(bool hidden)
	{
		// ShowCursor keeps a count: only change it when it changes.
		if (_isCursorHidden != hidden)
		{
			_isCursorHidden = hidden;
			ShowCursor(!hidden);
		}
	}

	private void UpdateTitle() => Title = ViewModel.Title.Length > 0 ? $"{ViewModel.Title} - Totoro" : "Totoro";

	/// <summary>Most of the screen, centred, at 16:9: the video fills the window, the title bar and the controls over it.</summary>
	private void PlaceOnScreen()
	{
		var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
		var width = (int)(area.Width * 0.7);
		AppWindow.ResizeClient(new SizeInt32(width, Math.Min(width * 9 / 16, (int)(area.Height * 0.9))));
		var size = AppWindow.Size;
		AppWindow.Move(new PointInt32(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2));
	}

	[LibraryImport("user32.dll")]
	private static partial int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool show);
}
