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

	// The seek bar's sections: the opening (and recap) and the ending, in Windows' yellow and teal; the episode in the accent colour.
	private static readonly Color s_openingColor = Color.FromArgb(255, 0xFF, 0xB9, 0x00);
	private static readonly Color s_endingColor = Color.FromArgb(255, 0x00, 0xB7, 0xC3);

	// The slider's thumb (SliderHorizontalThumbWidth), and the gap between the track's sections.
	private const double ThumbWidth = 18;
	private const double SectionGap = 3;

	private readonly DispatcherTimer _hideTimer = new() { Interval = s_hideControlsAfter };
	private bool _isPointerOverBar;
	private bool _isDraggingSeekBar;
	private bool _isUpdatingSeekBar;
	private bool _isCursorHidden;
	private bool _areControlsShown = true;

	// How far the title bar and the controls slide as they hide.
	private const float ChromeSlide = 24;

	public PlayerWindow(PlayerViewModel viewModel)
	{
		ViewModel = viewModel;
		TimeConverter = new SecondsConverter(this);
		InitializeComponent();

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
		UpdateTitle();

		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		_hideTimer.Tick += (_, _) => HideControlsIfIdle();
		// The slider handles the pointer itself; dragging it seeks when let go, not on every step.
		SeekBar.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => _isDraggingSeekBar = true), true);
		SeekBar.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => EndSeekBarDrag()), true);
		SeekBar.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => EndSeekBarDrag()), true);
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
		};
		SetChromeShown(true);
		ShowControls();
		ViewModel.UpdateViewportSize(VideoArea.ActualWidth, VideoArea.ActualHeight);
	}

	public PlayerViewModel ViewModel { get; }

	/// <summary>Shows the seek bar's seconds as a time and the section it's in, e.g. "1:23 · Opening".</summary>
	public IValueConverter TimeConverter { get; }

	// For x:Bind function bindings.

	public static bool IsBufferingShown(bool isBuffering, bool isStatusVisible) => isBuffering && !isStatusVisible;

	public static bool IsWorking(bool isStatusVisible, bool isError) => isStatusVisible && !isError;

	public static string PlayPauseName(bool isPlaying) => isPlaying ? "Pause (Space)" : "Play (Space)";

	public static string PlayPauseGlyph(bool isPlaying) => isPlaying ? "" : "";

	public static string MuteName(bool isMuted) => isMuted ? "Unmute (M)" : "Mute (M)";

	public static string VolumeGlyph(bool isMuted, double volume) => isMuted || volume <= 0 ? "" : volume < 34 ? "" : volume < 67 ? "" : "";

	public static string FullScreenName(bool isFullScreen) => isFullScreen ? "Exit full screen (F)" : "Full screen (F)";

	public static string FullScreenGlyph(bool isFullScreen) => isFullScreen ? "" : "";

	public static string VolumeText(double volume) => $"{volume:0}";

	public static string WindowTitle(string title) => title.Length > 0 ? $"{title} - Totoro" : "Totoro";

	/// <summary>The width of an episode's progress bar on its 128-wide thumbnail (inset 6 either side).</summary>
	public static double EpisodeProgressWidth(double progress) => Math.Max(4, progress * 116);

	/// <summary>e.g. "Up next · Episode 6".</summary>
	public static string UpNext(string title) => $"Up next · {title}";

	/// <summary>The "Up next" card: 16:9 with a thumbnail (its details over it); as tall as its details without one.</summary>
	public static double NextCardHeight(string image) => image.Length > 0 ? 360 * 9 / 16.0 : double.NaN;

	/// <summary>The episode controls (previous, next, skipping ahead, the episode list...): not for a short video, e.g. an opening.</summary>
	public static Visibility HiddenForClips(bool isClip) => isClip ? Visibility.Collapsed : Visibility.Visible;

	public static Visibility SkipButtonVisibility(bool isClip, bool isSkipButtonVisible) =>
		isClip || !isSkipButtonVisible ? Visibility.Collapsed : Visibility.Visible;

	public static Visibility VisibleIfAny(string text) => text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

	private void OnVideoInitialized(object? sender, InitializedEventArgs e) => _ = ViewModel.InitializeAsync(e.SwapChainOptions);

	private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
	{
		Activated -= OnFirstActivated;
		// So the keys work straight away.
		PlayPauseButton.Focus(FocusState.Programmatic);
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(PlayerViewModel.Position) when !_isDraggingSeekBar:
				SetSeekBar(() => SeekBar.Value = ViewModel.Position);
				UpdatePlayed(ViewModel.Position);
				break;
			case nameof(PlayerViewModel.Duration):
				SetSeekBar(() => SeekBar.Maximum = ViewModel.Duration);
				DrawSections();
				break;
			case nameof(PlayerViewModel.Chapters):
				DrawSections();
				break;
			case nameof(PlayerViewModel.Title):
				UpdateTitle();
				break;
			case nameof(PlayerViewModel.IsPlaying) when !ViewModel.IsPlaying:
				// Paused or over: the controls stay until it plays again.
				ShowControls();
				break;
			case nameof(PlayerViewModel.IsEpisodeListOpen):
				// Opened or closed from elsewhere (a key, picking an episode): the pop-up follows.
				if (ViewModel.IsEpisodeListOpen && !EpisodeFlyout.IsOpen)
				{
					ShowEpisodeList();
				}
				else if (!ViewModel.IsEpisodeListOpen && EpisodeFlyout.IsOpen)
				{
					EpisodeFlyout.Hide();
				}

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
		e.Handled = true;
		switch (e.Key)
		{
			case VirtualKey.Space or VirtualKey.K:
				ViewModel.PlayPauseCommand.Execute(null);
				break;
			case VirtualKey.Left or VirtualKey.J:
				_ = ViewModel.SeekAsync(TimeSpan.FromSeconds(ViewModel.Position) - s_arrowSeek);
				break;
			case VirtualKey.Right or VirtualKey.L:
				_ = ViewModel.SeekAsync(TimeSpan.FromSeconds(ViewModel.Position) + s_arrowSeek);
				break;
			case VirtualKey.Up:
				ViewModel.Volume = Math.Min(100, ViewModel.Volume + 5);
				break;
			case VirtualKey.Down:
				ViewModel.Volume = Math.Max(0, ViewModel.Volume - 5);
				break;
			case VirtualKey.M:
				ViewModel.ToggleMuteCommand.Execute(null);
				break;
			case VirtualKey.F or VirtualKey.F11:
				ViewModel.ToggleFullScreenCommand.Execute(null);
				break;
			case VirtualKey.E:
				ViewModel.ToggleEpisodeListCommand.Execute(null);
				break;
			case VirtualKey.Escape when ViewModel.IsFullScreen:
				ViewModel.IsFullScreen = false;
				break;
			case VirtualKey.PageUp or VirtualKey.S when ViewModel.HasOffer:
				ViewModel.AcceptOfferCommand.Execute(null);
				break;
			case VirtualKey.PageUp or VirtualKey.S when ViewModel.IsSkipButtonVisible:
				ViewModel.SkipAheadCommand.Execute(null);
				break;
			case VirtualKey.A:
				ViewModel.CycleAspectRatioCommand.Execute(null);
				break;
			case VirtualKey.N:
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

	private void OnEpisodesButtonClick(object sender, RoutedEventArgs e) => ViewModel.ToggleEpisodeListCommand.Execute(null);

	/// <summary>Opens the episode list above the controls (clear of the seek bar), centred over the Episodes button.</summary>
	private void ShowEpisodeList()
	{
		ShowControls();
		var button = EpisodesButton.TransformToVisual(Controls).TransformPoint(new Point(EpisodesButton.ActualWidth / 2, 0));
		EpisodeFlyout.ShowAt(Controls, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Top, Position = new Point(button.X, -8) });
	}

	private void OnEpisodeListOpened(object? sender, object e)
	{
		ViewModel.IsEpisodeListOpen = true;
		if (ViewModel.Episodes.FirstOrDefault(x => x.IsCurrent) is { } current)
		{
			EpisodeListView.ScrollIntoView(current, ScrollIntoViewAlignment.Leading);
		}
	}

	private void OnEpisodeListClosed(object? sender, object e) => ViewModel.IsEpisodeListOpen = false;

	private void OnVideoDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		// The double click's first click played or paused it: undo that.
		ViewModel.PlayPauseCommand.Execute(null);
		ViewModel.ToggleFullScreenCommand.Execute(null);
	}

	private void OnControlsPointerEntered(object sender, PointerRoutedEventArgs e) => _isPointerOverBar = true;

	private void OnControlsPointerExited(object sender, PointerRoutedEventArgs e) => _isPointerOverBar = false;

	private void OnEpisodeClick(object sender, ItemClickEventArgs e) => ViewModel.PlayEpisodeCommand.Execute(e.ClickedItem);

	private void OnSeekBarValueChanged(object sender, RangeBaseValueChangedEventArgs e)
	{
		UpdatePlayed(e.NewValue);
		// Seeks when it's moved by the user (by the keyboard, or a click); a drag seeks when let go.
		if (!_isUpdatingSeekBar && !_isDraggingSeekBar)
		{
			_ = ViewModel.SeekAsync(TimeSpan.FromSeconds(e.NewValue));
		}
	}

	private void EndSeekBarDrag()
	{
		if (_isDraggingSeekBar)
		{
			_isDraggingSeekBar = false;
			_ = ViewModel.SeekAsync(TimeSpan.FromSeconds(SeekBar.Value));
		}
	}

	private void SetSeekBar(Action set)
	{
		_isUpdatingSeekBar = true;
		try
		{
			set();
		}
		finally
		{
			_isUpdatingSeekBar = false;
		}
	}

	private void OnSeekTrackSizeChanged(object sender, SizeChangedEventArgs e) => DrawSections();

	/// <summary>
	/// Draws the seek bar's track in sections: the episode, and its opening, ending and recap in their own colours, with
	/// a gap between each. Drawn twice, dim and bright: the bright one is clipped to how far playback is.
	/// </summary>
	private void DrawSections()
	{
		TrackSections.Children.Clear();
		PlayedSections.Children.Clear();
		var width = SeekTrack.ActualWidth;
		if (width <= 0)
		{
			return;
		}

		foreach (var (start, end, type) in GetSections())
		{
			// The first and last reach the track's ends; the others meet with a gap.
			var left = start <= 0 ? 0 : ToTrackX(start, width) + SectionGap / 2;
			var right = end >= 1 ? width : ToTrackX(end, width) - SectionGap / 2;
			if (right - left < 1)
			{
				continue;
			}

			foreach (var (canvas, isPlayed) in new[] { (TrackSections, false), (PlayedSections, true) })
			{
				var section = new Rectangle
				{
					Width = right - left,
					Height = SeekTrack.ActualHeight,
					RadiusX = 2,
					RadiusY = 2,
					Fill = new SolidColorBrush(SectionColor(type, isPlayed)),
				};
				Canvas.SetLeft(section, left);
				canvas.Children.Add(section);
			}
		}

		UpdatePlayed(_isDraggingSeekBar ? SeekBar.Value : ViewModel.Position);
	}

	/// <summary>Brightens the track up to <paramref name="seconds"/>.</summary>
	private void UpdatePlayed(double seconds)
	{
		var width = SeekTrack.ActualWidth;
		var played = ViewModel.Duration > 0 && width > 0 ? ToTrackX(Math.Clamp(seconds / ViewModel.Duration, 0, 1), width) : 0;
		PlayedSections.Clip = new RectangleGeometry { Rect = new Rect(0, 0, played, SeekTrack.ActualHeight) };
	}

	/// <summary>The track's sections, as fractions of the video: the episode where there's no segment.</summary>
	private List<(double Start, double End, MediaSegmentType? Type)> GetSections()
	{
		var duration = ViewModel.Duration;
		var chapters = ViewModel.Chapters;
		if (duration <= 0 || chapters.Count == 0)
		{
			return [(0, 1, null)];
		}

		List<(double, double, MediaSegmentType?)> sections = [];
		for (var i = 0; i < chapters.Count; i++)
		{
			var start = Math.Clamp(chapters[i].Start.TotalSeconds / duration, 0, 1);
			var end = Math.Clamp((i + 1 < chapters.Count ? chapters[i + 1].Start.TotalSeconds : duration) / duration, 0, 1);
			sections.Add((i == 0 ? 0 : start, end, Enum.TryParse<MediaSegmentType>(chapters[i].Title, out var type) ? type : null));
		}

		return sections;
	}

	// Where a fraction of the video is on the track: where the slider's thumb centre is for it, half a thumb in from
	// each end.
	private static double ToTrackX(double fraction, double width) => ThumbWidth / 2 + fraction * (width - ThumbWidth);

	private static Color SectionColor(MediaSegmentType? type, bool isPlayed)
	{
		var color = type switch
		{
			MediaSegmentType.Opening or MediaSegmentType.Recap => s_openingColor,
			MediaSegmentType.Ending => s_endingColor,
			_ => isPlayed ? (Color)Application.Current.Resources["SystemAccentColorLight2"] : Colors.White,
		};
		// Unplayed is dimmer; the episode's unplayed part is a plain grey track.
		color.A = isPlayed ? (byte)255 : type is null ? (byte)0x5C : (byte)0x80;
		return color;
	}

	private void OnTracksMenuOpening(object sender, object e)
	{
		var menu = (MenuFlyout)sender;
		menu.Items.Clear();
		var tracks = ViewModel.GetTracks();
		foreach (var (kind, label) in new[] { (MediaTrackKind.Audio, "Audio"), (MediaTrackKind.Subtitle, "Subtitles") })
		{
			var group = new MenuFlyoutSubItem { Text = label };
			var ofKind = tracks.Where(x => x.Kind == kind).ToList();
			if (ofKind.Count == 0)
			{
				group.Items.Add(new MenuFlyoutItem { Text = "None", IsEnabled = false });
			}

			foreach (var track in ofKind)
			{
				var item = new RadioMenuFlyoutItem { Text = track.Name, IsChecked = track.IsSelected, GroupName = label };
				item.Click += (_, _) => _ = ViewModel.SelectTrackAsync(kind, track.Id);
				group.Items.Add(item);
			}

			menu.Items.Add(group);
		}
	}

	private void OnAspectRatioMenuOpening(object sender, object e)
	{
		var menu = (MenuFlyout)sender;
		menu.Items.Clear();
		var current = ViewModel.AspectRatio;
		foreach (var mode in Enum.GetValues<PlayerAspectRatio>())
		{
			var item = new RadioMenuFlyoutItem
			{
				Text = PlayerViewModel.PlayerAspectRatioText(mode),
				IsChecked = current == mode,
				GroupName = "AspectRatio",
			};
			item.Click += (_, _) => ViewModel.SetAspectRatio(mode);
			menu.Items.Add(item);
		}
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
		if (!ViewModel.IsPlaying || _isPointerOverBar || _isDraggingSeekBar || ViewModel.IsEpisodeListOpen ||
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

	private sealed partial class SecondsConverter(PlayerWindow window) : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value is not double seconds)
			{
				return "";
			}

			var viewModel = window.ViewModel;
			var time = PlayerViewModel.Format(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(viewModel.Duration));
			return viewModel.SegmentAt(seconds) is { } segment ? $"{time} · {segment}" : time;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
	}
}
