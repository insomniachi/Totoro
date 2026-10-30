using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.Playback;
using Totoro.ViewModels.Chat;

namespace Totoro.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IRecipient<AnimeDatabaseChangedMessage>, IRecipient<OtherListSyncFailedMessage>
{
	private readonly IReadOnlyList<AccountViewModel> _accounts;
	private readonly IFactory<IAnimeMetadataProvider, Guid> _databases;
	private readonly IFactory<Core.Modules.Manga.IMangaMetadataProvider, Guid> _mangaDatabases;
	private readonly IReadOnlyList<NavigationItemViewModel> _allItems;
	private CancellationTokenSource? _search;

	public MainWindowViewModel(IEnumerable<NavigationItemViewModel> navigationItems, IEnumerable<IAccount> accounts, IPreferences preferences, WatchService playback, ChatViewModel chat, UpdateService updates, IFactory<IAnimeMetadataProvider, Guid> databases, IFactory<Core.Modules.Manga.IMangaMetadataProvider, Guid> mangaDatabases)
	{
		_databases = databases;
		_mangaDatabases = mangaDatabases;
		Playback = playback;
		Chat = chat;
		Updates = updates;
		Updates.PropertyChanged += (_, e) =>
		{
			// Offered when one's found; and shown while it downloads, or if that fails.
			if (e.PropertyName == nameof(UpdateService.State))
			{
				IsUpdateBarOpen = Updates.State is UpdateState.Available or UpdateState.Downloading or UpdateState.Installing ||
								  Updates.State == UpdateState.Failed && Updates.Available is not null;
			}
		};
		_ = Updates.CheckOnStartupAsync();
		_allItems = [.. navigationItems];
		// The app opens in the startup mode, on that mode's page named as the startup page (Library for Library); a
		// startup page only one mode has (e.g. Schedule) opens in its mode.
		Mode = preferences.StartupMode;
		var startup = _allItems.FirstOrDefault(x => x.Key == preferences.StartupPage);
		if (startup is { Mode: { } startupMode } && startupMode != Mode && !_allItems.Any(x => x.Mode == Mode && x.Label == startup.Label))
		{
			Mode = startupMode;
		}

		NavigationItems = ItemsFor(Mode, footer: false);
		FooterNavigationItems = ItemsFor(Mode, footer: true);

		StartupItem = startup is not null && NavigationItems.Contains(startup) ? startup
			: NavigationItems.FirstOrDefault(x => x.Label == startup?.Label) ?? NavigationItems.FirstOrDefault();
		CurrentItem = StartupItem;

		_accounts = [.. accounts.Select(x => new AccountViewModel(x))];
		ShowAccount(preferences.SelectedAnimeDatabase);
		WeakReferenceMessenger.Default.RegisterAll(this);
	}

	[ObservableProperty]
	public partial List<NavigationItemViewModel> NavigationItems { get; set; }

	/// <summary>Anime or manga: which of the menu's pages show, and which kind the title bar's search lists first.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ModeIndex))]
	public partial MediaMode Mode { get; private set; }

	/// <summary><see cref="Mode"/> as the title bar's Anime | Manga switch's index.</summary>
	public int ModeIndex
	{
		get => (int)Mode;
		set => SwitchMode((MediaMode)Math.Clamp(value, 0, 1));
	}

	/// <summary>
	/// The page to show after switching mode (the new mode's page of the same name, e.g. Library for Library, else its
	/// first); null until the mode is switched.
	/// </summary>
	[ObservableProperty]
	public partial NavigationItemViewModel? ModeSwitchItem { get; private set; }

	// The menu gets new lists rather than hiding items: an ItemsRepeater whose first item is collapsed shows nothing.
	private List<NavigationItemViewModel> ItemsFor(MediaMode mode, bool footer) =>
		[.. _allItems.Where(x => x.IsFooter == footer && (x.Mode is null || x.Mode == mode))];

	/// <param name="current">The page showing, whose counterpart is shown in the new mode.</param>
	public void SwitchMode(MediaMode mode, NavigationItemViewModel? current = null)
	{
		if (mode == Mode)
		{
			return;
		}

		current ??= CurrentItem;
		Mode = mode;
		NavigationItems = ItemsFor(mode, footer: false);
		FooterNavigationItems = ItemsFor(mode, footer: true);
		ModeSwitchItem = current is { Mode: null } ? current
			: NavigationItems.FirstOrDefault(x => x.Label == current?.Label) ?? NavigationItems.FirstOrDefault();
	}

	/// <summary>The menu item selected, as the window last said.</summary>
	public NavigationItemViewModel? CurrentItem { get; set; }

	[ObservableProperty]
	public partial List<NavigationItemViewModel> FooterNavigationItems { get; set; }

	/// <summary>The menu item to select when the app starts; see <see cref="StartupPagePreferences"/>.</summary>
	public NavigationItemViewModel? StartupItem { get; }

	/// <summary>What playing an episode is up to, for the status bar.</summary>
	public WatchService Playback { get; }

	/// <summary>The assistant, in a panel beside the pages.</summary>
	public ChatViewModel Chat { get; }

	/// <summary>Updating Totoro: a newer version is offered in a bar over the content.</summary>
	public UpdateService Updates { get; }

	[ObservableProperty]
	public partial bool IsUpdateBarOpen { get; set; }

	/// <summary>The account of the selected anime database, shown in the title bar.</summary>
	[ObservableProperty]
	public partial AccountViewModel? Account { get; private set; }

	public void Receive(AnimeDatabaseChangedMessage message)
	{
		ShowAccount(message.ModuleId);
	}

	/// <summary>Why a change to the user's list wasn't made on their other list, e.g. MyAnimeList; empty, never null, when it was.</summary>
	[ObservableProperty]
	public partial string SyncWarning { get; private set; } = "";

	[ObservableProperty]
	public partial bool IsSyncWarningOpen { get; set; }

	public void Receive(OtherListSyncFailedMessage message)
	{
		SyncWarning = message.Message;
		IsSyncWarningOpen = true;
	}

	/// <summary>The title bar's search: anime and manga matching what's typed, whatever the mode (its own kind first).</summary>
	[ObservableProperty]
	public partial List<MediaSuggestion> SearchResults { get; set; } = [];

	// Of each kind, at most.
	private const int SuggestionsPerKind = 6;

	/// <summary>Searches the selected anime and manga databases, as the user types in the title bar.</summary>
	public async Task SearchAsync(string query)
	{
		_search?.Cancel();
		var search = _search = new CancellationTokenSource();
		query = query.Trim();
		if (query.Length < 2)
		{
			SearchResults = [];
			return;
		}

		try
		{
			// Searches after typing pauses.
			await Task.Delay(300, search.Token);
			var anime = SearchOrNoneAsync(async () => _databases.CreateDefault() is { } database ? [.. await database.SearchAnimeAsync(query)] : []);
			var manga = SearchOrNoneAsync(async () => _mangaDatabases.CreateDefault() is { } database ? [.. await database.SearchMangaAsync(query, search.Token)] : []);
			List<IMediaModel>[] kinds = Mode == MediaMode.Manga ? [await manga, await anime] : [await anime, await manga];
			if (!search.IsCancellationRequested)
			{
				// Taking turns, the mode's kind first, so both are in sight before the list has to scroll.
				SearchResults = [.. Enumerable.Range(0, SuggestionsPerKind)
											  .SelectMany(i => kinds.Select(x => x.ElementAtOrDefault(i)))
											  .OfType<IMediaModel>()
											  .Select(x => new MediaSuggestion(x))];
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	// Offline, or a database is down: none of its kind rather than a crash from the title bar.
	private static async Task<List<IMediaModel>> SearchOrNoneAsync(Func<Task<List<IMediaModel>>> search)
	{
		try
		{
			return await search();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return [];
		}
	}

	private void ShowAccount(Guid databaseId)
	{
		// An unset (or unknown) database means the first one, as in the database factory and Settings.
		Account = _accounts.FirstOrDefault(x => x.Id == databaseId) ?? _accounts.FirstOrDefault();
	}
}
