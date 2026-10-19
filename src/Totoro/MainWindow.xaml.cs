using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Services;
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
		// The taskbar's and Alt+Tab's icon.
		AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Totoro.ico"));
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

	private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
		{
			_ = ViewModel?.SearchAsync(sender.Text);
		}
	}

	private async void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		// A suggestion picked, or Enter with suggestions showing: the first one.
		var anime = (args.ChosenSuggestion as ChatAnimeItem ?? ViewModel?.SearchResults.FirstOrDefault())?.Anime;
		if (anime is not null && Navigators.TryGetNavigator(PaneNavigatorKey, out var pane))
		{
			sender.Text = "";
			ViewModel!.SearchResults = [];
			await pane.NavigateToDataAsync(anime);
		}
	}

	private const string PaneNavigatorKey = "pane";

	/// <summary>The app's version, e.g. "2.1.0", for the title bar: the release's (see .github/workflows), without the commit's hash.</summary>
	public static string Version { get; } =
		(typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "")
		.Split('+')[0];

	// For the update bar's x:Bind function bindings.

	// x:Bind calls these with null too, e.g. when a check finds no newer version (Available goes back to null).

	public static string UpdateTitle(AppRelease? release) => release is null ? "" : $"Totoro {release.Version} is available";

	public static Uri? ReleasePage(AppRelease? release) => release?.Page;

	public static string UpdateMessage(UpdateState state, string error) => state switch
	{
		UpdateState.Downloading => "Downloading it…",
		UpdateState.Installing => "Installing it: Totoro closes, and starts again once it's done.",
		UpdateState.Failed => error,
		_ => "Update to install it: Totoro closes, and starts again once it's done.",
	};

	public static Visibility VisibleIfPositive(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

	public static InfoBarSeverity UpdateSeverity(UpdateState state) => state == UpdateState.Failed ? InfoBarSeverity.Error : InfoBarSeverity.Informational;

	public static Visibility VisibleIfUpdatable(UpdateState state) => state is UpdateState.Available or UpdateState.Failed ? Visibility.Visible : Visibility.Collapsed;

	public static Visibility VisibleWhileUpdating(UpdateState state) => state is UpdateState.Downloading or UpdateState.Installing ? Visibility.Visible : Visibility.Collapsed;

	public static bool IsInstalling(UpdateState state) => state == UpdateState.Installing;

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

	// The notch on the playback position where the episode counts as watched (see WatchService.CountsAtFraction): the
	// columns either side of it, and only until it's been marked.

	public static Visibility VisibleWhileNotMarked(double countsAt, bool isMarked) => countsAt >= 0 && !isMarked ? Visibility.Visible : Visibility.Collapsed;

	public static GridLength Before(double countsAt) => new(Math.Clamp(countsAt, 0, 1), GridUnitType.Star);

	public static GridLength After(double countsAt) => new(1 - Math.Clamp(countsAt, 0, 1), GridUnitType.Star);
}
