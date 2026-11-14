using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Totoro.Core.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.System;

namespace Totoro.UI.Controls;

/// <summary>A song's card, which plays it: in the Music tab, and in the chat. See <see cref="SongItem"/>.</summary>
public sealed partial class SongCard : UserControl
{
	// The video full screen: the window goes full screen, and the card's player moves into a popup over all of it (one
	// element: two sharing a MediaPlayer crash). WinUI's own IsFullWindow leaves the video where it is on desktop.
	private Popup? _fullScreen;
	private AppWindow? _window;
	private SplitView? _pane;

	public SongCard()
	{
		InitializeComponent();
		// Left playing, it would go on being heard after the card's gone.
		Unloaded += (_, _) =>
		{
			ExitFullScreen();
			Song?.IsVideoOpen = false;
		};
	}

	[GeneratedDependencyProperty]
	public partial SongItem? Song { get; set; }

	public IMediaPlaybackSource? VideoSource(bool isOpen, Uri video)
	{
		if (!isOpen)
		{
			return null;
		}

		var source = MediaSource.CreateFromUri(video);
		// Its length picks its lyrics; then they follow its player.
		source.OpenOperationCompleted += (sender, _) => DispatcherQueue.TryEnqueue(() =>
		{
			VideoLyrics.Video = Video.MediaPlayer;
			Song?.VideoOpened(sender.Duration ?? TimeSpan.Zero);
		});
		return source;
	}

	private void OnVideoSizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (_fullScreen is null && e.NewSize.Width != e.PreviousSize.Width)
		{
			Video.Height = e.NewSize.Width * 9 / 16;
		}
	}

	private void OnFullScreenClick(object sender, RoutedEventArgs e) => EnterFullScreen();

	private void OnVideoDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (_fullScreen is null)
		{
			EnterFullScreen();
		}
		else
		{
			ExitFullScreen();
		}
	}

	private void EnterFullScreen()
	{
		if (_fullScreen is not null || Video.MediaPlayer is null
			|| XamlRoot?.ContentIslandEnvironment is not { } island || AppWindow.GetFromWindowId(island.AppWindowId) is not { } window)
		{
			return;
		}

		var exit = new Button
		{
			Margin = new Thickness(16),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Top,
			Content = new FontIcon { FontSize = 14, Glyph = "" },
		};
		AutomationProperties.SetName(exit, "Exit full screen");
		ToolTipService.SetToolTip(exit, "Exit full screen (Esc)");
		exit.Click += (_, _) => ExitFullScreen();
		var root = new Grid { Background = new SolidColorBrush(Colors.Black) };
		root.KeyDown += (_, e) =>
		{
			if (e.Key == VirtualKey.Escape)
			{
				e.Handled = true;
				ExitFullScreen();
			}
		};
		root.Loaded += (_, _) => exit.Focus(FocusState.Programmatic);

		_window = window;
		// Focus leaving a light-dismiss pane (the song page's) would close it, and the video with it.
		_pane = this.FindAscendant<SplitView>();
		_pane?.PaneClosing += OnPaneClosing;
		VideoPanel.Children.Remove(Video);
		Video.Height = double.NaN;
		root.Children.Add(Video);
		root.Children.Add(exit);
		_fullScreen = new Popup { XamlRoot = XamlRoot, Child = root };
		FitFullScreen();
		_fullScreen.IsOpen = true;
		XamlRoot.Changed += OnXamlRootChanged;
		window.SetPresenter(AppWindowPresenterKind.FullScreen);
		window.Changed += OnWindowChanged;
	}

	private void ExitFullScreen()
	{
		if (_fullScreen is null)
		{
			return;
		}

		_window!.Changed -= OnWindowChanged;
		_fullScreen.XamlRoot.Changed -= OnXamlRootChanged;
		((Panel)_fullScreen.Child).Children.Clear();
		_fullScreen.IsOpen = false;
		_fullScreen = null;
		VideoPanel.Children.Insert(0, Video);
		FullScreenButton.Focus(FocusState.Programmatic);
		// The pane light-dismisses when the window resizes, which it does a moment after leaving full screen: hold it
		// open until then.
		var (window, pane) = (_window, _pane);
		_window = null;
		_pane = null;
		void OnResized(AppWindow sender, AppWindowChangedEventArgs args)
		{
			if (args.DidSizeChange)
			{
				window.Changed -= OnResized;
				DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => pane?.PaneClosing -= OnPaneClosing);
			}
		}

		window.Changed += OnResized;
		window.SetPresenter(AppWindowPresenterKind.Overlapped);
	}

	private void FitFullScreen()
	{
		if (_fullScreen?.Child is FrameworkElement root)
		{
			root.Width = _fullScreen.XamlRoot.Size.Width;
			root.Height = _fullScreen.XamlRoot.Size.Height;
		}
	}

	private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => FitFullScreen();

	private void OnPaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args) => args.Cancel = true;

	private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
	{
		if (args.DidPresenterChange && sender.Presenter.Kind != AppWindowPresenterKind.FullScreen)
		{
			ExitFullScreen();
		}
	}

	// The choice ticked, English only where the song has English lyrics, and readings only for lyrics in Japanese script
	// (lyrics in romaji show as they are: "Original").
	private void OnReadingsOpening(object sender, object e)
	{
		var japanese = Song?.HasJapanese == true;
		foreach (var item in ((MenuFlyout)sender).Items.OfType<RadioMenuFlyoutItem>())
		{
			var choice = Enum.Parse<LyricReadings>((string)item.Tag);
			if (choice == LyricReadings.None)
			{
				item.Text = japanese ? "Japanese only" : "Original";
			}

			item.IsEnabled = choice switch
			{
				LyricReadings.English => Song?.HasEnglish == true,
				LyricReadings.None => true,
				_ => japanese,
			};
			item.IsChecked = item.Text == Song?.ReadingsName;
		}
	}

	private void OnReadingsClick(object sender, RoutedEventArgs e)
	{
		Song?.SetReadings(Enum.Parse<LyricReadings>((string)((FrameworkElement)sender).Tag));
	}

	public static Visibility VisibleIfAny(string text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
}
