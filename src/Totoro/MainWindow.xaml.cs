using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Playback;
using Totoro.UI.Navigation;
using Totoro.ViewModels;

namespace Totoro;

/// <summary>
/// The app's window: the navigation menu and pages, the anime details pane, the title bar (with the account) and the
/// playback status bar.
/// </summary>
public sealed partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		ExtendsContentIntoTitleBar = true;
		SetTitleBar(AppTitleBar);
		AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
		// The back button is hidden (not just disabled) until there is somewhere to go back to.
		AppTitleBar.BackRequested += async (_, _) =>
		{
			if (Navigators.TryGetNavigator(MainNavigatorKey, out var navigator))
			{
				await navigator.GoBackAsync();
			}
		};

		// The "main" navigator is created when the NavigationView loads, after this constructor runs.
		Navigators.NavigatorAdded += OnNavigatorAdded;
	}

	private const string MainNavigatorKey = "main";

	private void OnNavigatorAdded(object? sender, NavigatorAddedEventArgs e)
	{
		if (e.Key != MainNavigatorKey)
		{
			return;
		}

		Navigators.NavigatorAdded -= OnNavigatorAdded;
		var navigator = e.Navigator;
		navigator.Navigated += (_, _) => AppTitleBar.IsBackButtonVisible = navigator.CanGoBack;

		// Selecting the item makes the navigator navigate to it, and keeps the menu highlight in sync.
		NavView.SelectedItem = ViewModel?.StartupItem;
	}

	public MainWindowViewModel? ViewModel { get; set; }

	// For the status bar's x:Bind function bindings.

	public static InfoBarSeverity ToSeverity(PlaybackStatusKind kind) => kind switch
	{
		PlaybackStatusKind.Success => InfoBarSeverity.Success,
		PlaybackStatusKind.Error => InfoBarSeverity.Error,
		_ => InfoBarSeverity.Informational,
	};

	/// <summary>The progress bar and Cancel, while getting an episode ready.</summary>
	public static Visibility VisibleWhileWorking(PlaybackStatusKind kind) => kind == PlaybackStatusKind.Progress ? Visibility.Visible : Visibility.Collapsed;

	// For the title bar's now-playing button.

	public static bool IsWorking(PlaybackStatusKind kind) => kind == PlaybackStatusKind.Progress;

	public static Visibility VisibleWhilePlayingKind(PlaybackStatusKind kind) => kind == PlaybackStatusKind.Playing ? Visibility.Visible : Visibility.Collapsed;

	/// <summary>While an episode is being got ready or plays, but the status bar was closed.</summary>
	public static Visibility VisibleWhileStatusHidden(bool isActive, bool isOpen) => isActive && !isOpen ? Visibility.Visible : Visibility.Collapsed;

	public static string NowPlayingText(PlaybackStatusKind kind) => kind == PlaybackStatusKind.Progress ? "Getting ready…" : "Now playing";

	/// <summary>The button's tooltip and accessible name, e.g. "Frieren · Episode 5: show how it's going".</summary>
	public static string NowPlayingName(string title) => string.IsNullOrEmpty(title) ? "Show what's playing" : $"{title}: show how it's going";

	/// <summary>The playback position, while an episode plays (see WatchService.PlaybackFraction).</summary>
	public static Visibility VisibleWhilePlaying(double fraction) => fraction >= 0 ? Visibility.Visible : Visibility.Collapsed;
}
