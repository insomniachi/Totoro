using System.Reflection;
using CommunityToolkit.WinUI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
		Navigators.NavigatorAdded += OnPaneNavigatorAdded;
	}

	private const string MainNavigatorKey = "main";

	// The side pane's back button: there while it has a page to go back to (its history clears when it closes).
	private void OnPaneNavigatorAdded(object? sender, NavigatorAddedEventArgs e)
	{
		if (e.Key != PaneNavigatorKey)
		{
			return;
		}

		Navigators.NavigatorAdded -= OnPaneNavigatorAdded;
		var pane = e.Navigator;
		pane.Navigated += (_, _) => PaneBackButton.Visibility = pane.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
		PaneBackButton.Click += async (_, _) => await pane.GoBackAsync();
	}

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
		if (ViewModel is { } viewModel)
		{
			viewModel.PropertyChanged += (_, e) =>
			{
				// After the menu has its new mode's items: selecting one that isn't in it yet does nothing.
				if (e.PropertyName == nameof(MainWindowViewModel.ModeSwitchItem) && viewModel.ModeSwitchItem is { } item)
				{
					DispatcherQueue.TryEnqueue(() => NavView.SelectedItem = item);
				}
			};

			if (viewModel.ShowTour)
			{
				DispatcherQueue.TryEnqueue(() => StartTour(viewModel));
			}
		}
	}

	// The first-run tour: what each part of the window is for, one tip at a time.

	private record TourStep(Func<FrameworkElement?> Target, string Title, string Text);

	/// <summary>What each menu page is for, by its <see cref="Core.Navigation.NavigationItemViewModel.Name"/>; pages not here (Agent tools) get no tip.</summary>
	private static readonly Dictionary<string, string> s_pageTips = new()
	{
		["Discover"] = "Find something new to watch: what's trending, what airs today, what starts next season, and sequels you missed.",
		["Library"] = "Your anime list, by status: watching, plan to watch, completed, on hold and dropped. Pick one to see its details and play its episodes.",
		["Search"] = "Find anime by title, season, year, score and genre, or paste a screenshot (Ctrl+V) to find which anime, episode and moment it's from.",
		["Schedule"] = "When the next episodes of the anime airing now come out.",
		["Watch"] = "Search a plugin directly, pick a show, an episode and a stream, and play it. Handy for anything that isn't on your list.",
		["Downloads"] = "Episodes downloading and downloaded, to watch offline. The number on it is how many are downloading.",
		["Discover (manga)"] = "Find something new to read: popular and new manga.",
		["Library (manga)"] = "Your manga list, by status: reading, plan to read, completed, on hold and dropped.",
		["Search (manga)"] = "Find manga by title, year, score and genre.",
		["Read (manga)"] = "Search a plugin directly, pick a manga and a chapter, and read it. Handy for anything that isn't on your list.",
		["Plugins"] = "Add, update and remove plugins: where Totoro finds anime to watch and manga to read. The number on it is how many have an update.",
		["Settings"] = "Set the page Totoro opens on, your accounts, your player, playback and downloads.",
	};

	private TourStep[] _tour = [];
	private UIElement? _tourHero;
	private int _tourStep;

	private void StartTour(MainWindowViewModel viewModel)
	{
		_tourHero ??= TourTip.HeroContent;
		_tour =
		[
			// No target: in the middle of the window, with Totoro's picture.
			new(() => null, "Welcome to Totoro", "Here's a quick look around. It takes a minute, and you can skip it any time."),
			new(() => ModeSwitch, "Anime or manga", "Switch between your anime and your manga. The menu's pages change to match."),
			new(() => SearchBox, "Search anything", "Find anime, manga, characters, voice actors or songs by name. Pick what to search with the icon at its left, or type a:, m:, c:, v: or s: first."),
			// Each page in the menu (the startup mode's), Settings last; its item is looked up when its tip shows.
			.. viewModel.NavigationItems.Concat(viewModel.FooterNavigationItems)
				.Where(x => x.IsVisible && s_pageTips.ContainsKey(x.Name))
				.Select(x => new TourStep(() => NavView.ContainerFromMenuItem(x) as FrameworkElement, x.Label, s_pageTips[x.Name])),
			new(() => AccountButton, "Your account", "Sign in to keep your list in sync as you watch and read. Watch together with friends from here too."),
			// Collapsed when there's no assistant: no tip pointing at nothing.
			.. viewModel.Chat.IsAvailable
				? [new TourStep(() => AssistantButton, "Ask the assistant", "Ask for recommendations, or to find, play or update something for you.")]
				: Array.Empty<TourStep>(),
		];
		_tourStep = 0;
		ShowTourStep();
	}

	private void ShowTourStep()
	{
		// A menu item with no container yet (in the pane's overflow, say) is skipped: a tip there would point at nothing.
		var target = _tour[_tourStep].Target();
		while (target is null && _tourStep > 0 && _tourStep < _tour.Length - 1)
		{
			target = _tour[++_tourStep].Target();
		}

		var step = _tour[_tourStep];
		TourTip.Target = target;
		TourTip.PreferredPlacement = TourTip.Target is null ? TeachingTipPlacementMode.Center : TeachingTipPlacementMode.Auto;
		TourTip.HeroContent = _tourStep == 0 ? _tourHero : null;
		TourTip.Title = step.Title;
		TourTip.Subtitle = step.Text;
		TourTip.ActionButtonContent = _tourStep == _tour.Length - 1 ? "Got it" : "Next";
		TourTip.IsOpen = true;
	}

	private void OnTourNext(TeachingTip sender, object args)
	{
		sender.IsOpen = false;
		if (++_tourStep == _tour.Length)
		{
			ViewModel?.FinishTour();
		}
	}

	// The next step once the tip has closed: opened again before then, it stays where it was, at the last step's target.
	private void OnTourTipClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
	{
		if (_tourStep < _tour.Length && args.Reason == TeachingTipCloseReason.Programmatic)
		{
			ShowTourStep();
		}
	}

	private void OnTourSkip(TeachingTip sender, object args)
	{
		_tourStep = _tour.Length;
		sender.IsOpen = false;
		ViewModel?.FinishTour();
	}

	private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
	{
		// Null too (e.g. after leaving the reader for a page with no item), so a mode switch doesn't reopen the reader's item.
		if (ViewModel is not null)
		{
			ViewModel.CurrentItem = args.SelectedItem as Core.Navigation.NavigationItemViewModel;
		}
	}

	public MainWindowViewModel? ViewModel { get; set; }

	// The pane slides in without laying anything out, so a page laid out as it opened thinks it's off screen: its
	// virtualizing controls (a SelectorBar's tabs) show only their first item. Laid out again now it's in place, they see it.
	private void OnPaneOpened(SplitView sender, object args)
	{
		foreach (var element in Pane.FindDescendants().OfType<FrameworkElement>())
		{
			element.InvalidateMeasure();
		}
	}

	private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput || ViewModel is not { } viewModel)
		{
			return;
		}

		// A prefix (e.g. "c:") picks the category, and goes: the box keeps only what's searched for.
		var (category, query) = SearchCategories.Parse(sender.Text);
		if (category is { } picked)
		{
			viewModel.SearchCategory = picked;
			sender.Text = query;
			if (sender.FindDescendant<TextBox>() is { } box)
			{
				box.SelectionStart = query.Length;
			}
		}

		_ = viewModel.SearchAsync(sender.Text);
	}

	private async void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		// A suggestion picked, or Enter with suggestions showing: the first one.
		var data = (args.ChosenSuggestion as SearchSuggestion ?? ViewModel?.SearchResults.FirstOrDefault())?.Data;
		if (data is not null && Navigators.TryGetNavigator(PaneNavigatorKey, out var pane))
		{
			sender.Text = "";
			ViewModel!.SearchResults = [];
			await pane.NavigateToDataAsync(data);
			// Opened from outside the pane: it starts afresh, with nothing to go back to (Back is for pages opened from it).
			Pane.BackStack.Clear();
			PaneBackButton.Visibility = Visibility.Collapsed;
		}
	}

	// The box's text starts after the category icon over its left edge (its placeholder too: both follow its padding).
	private void OnSearchBoxLoaded(object sender, RoutedEventArgs e)
	{
		if (SearchBox.FindDescendant<TextBox>() is { } box)
		{
			box.Padding = new Thickness(36, box.Padding.Top, box.Padding.Right, box.Padding.Bottom);
		}
	}

	// The categories, the one searched for checked, each with its prefix at the right as a menu shows a shortcut.
	private void OnSearchCategoryMenuOpening(object? sender, object e)
	{
		SearchCategoryMenu.Items.Clear();
		foreach (var category in SearchCategories.All)
		{
			var item = new RadioMenuFlyoutItem
			{
				Text = SearchCategories.Name(category),
				Icon = new FontIcon { Glyph = SearchCategoryGlyph(category) },
				KeyboardAcceleratorTextOverride = SearchCategories.Prefix(category),
				IsChecked = category == ViewModel?.SearchCategory,
				GroupName = "SearchCategory",
			};
			item.Click += (_, _) =>
			{
				if (ViewModel is { } viewModel)
				{
					viewModel.SearchCategory = category;
					_ = viewModel.SearchAsync(SearchBox.Text);
					SearchBox.Focus(FocusState.Programmatic);
				}
			};
			SearchCategoryMenu.Items.Add(item);
		}
	}

	/// <summary>The category's icon: the mode switch's for anime and manga.</summary>
	public static string SearchCategoryGlyph(SearchCategory category) => category switch
	{
		SearchCategory.Anime => "",
		SearchCategory.Manga => "",
		SearchCategory.Character => "",
		SearchCategory.VoiceActor => "",
		_ => "",
	};

	/// <summary>e.g. "Search voice actors".</summary>
	public static string SearchPlaceholder(SearchCategory category) => $"Search {SearchCategories.Name(category).ToLowerInvariant()}";

	public static string SearchCategoryTip(SearchCategory category) =>
		$"Searching {SearchCategories.Name(category).ToLowerInvariant()}. Pick what to search, or type a:, m:, c:, v: or s: first.";

	private const string PaneNavigatorKey = "pane";

	private bool IsFullScreen => AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;

	private void OnFullScreenInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
	{
		args.Handled = true;
		SetFullScreen(!IsFullScreen);
	}

	private void OnNetworkClick(object sender, RoutedEventArgs e) => _ = Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:network-status"));

	// Watch together, Leave and Catch up: out of the way of what they open (the dialog, the player).
	private void OnWatchTogetherClick(object sender, RoutedEventArgs e) => AccountFlyout.Hide();

	private void OnRoomChatKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == Windows.System.VirtualKey.Enter)
		{
			e.Handled = true;
			ViewModel?.SendRoomChatCommand.Execute(null);
		}
	}

	private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
	{
		args.Handled = IsFullScreen;
		if (IsFullScreen)
		{
			SetFullScreen(false);
		}
	}

	private void SetFullScreen(bool isFullScreen)
	{
		AppWindow.SetPresenter(isFullScreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped);
	}

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

	public static IconElement? CreateIcon(Func<object>? icon) => icon?.Invoke() as IconElement;

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

	/// <summary>The progress bar fills during the countdown to the next episode; otherwise it can't say how long.</summary>
	public static bool IsIndeterminate(double countdownFraction) => countdownFraction < 0;

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
