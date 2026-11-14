using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeDatabase.Anilist;
using Totoro.Playback;
using Totoro.ViewModels.Chat;

namespace Totoro.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IRecipient<AnimeDatabaseChangedMessage>, IRecipient<OtherListSyncFailedMessage>
{
	private readonly IReadOnlyList<AccountViewModel> _accounts;
	private readonly IDialogService _dialogs;
	private readonly IFactory<IAnimeMetadataProvider, Guid> _databases;
	private readonly IFactory<Core.Modules.Manga.IMangaMetadataProvider, Guid> _mangaDatabases;
	private readonly IReadOnlyList<NavigationItemViewModel> _allItems;
	private readonly AnilistPeople _people;
	private readonly AnimeSongService _songs;
	private CancellationTokenSource? _search;
	private readonly IPreferences _preferences;
	private const string TourSeenKey = "TourSeen";

	/// <summary>The first-run tour of the window's teaching tips: until it's been finished or skipped once.</summary>
	public bool ShowTour { get; }

	public void FinishTour() => _preferences.SaveKey(TourSeenKey, true);

	public MainWindowViewModel(IEnumerable<NavigationItemViewModel> navigationItems, IEnumerable<IAccount> accounts, IPreferences preferences, WatchService playback, ChatViewModel chat, UpdateService updates, IFactory<IAnimeMetadataProvider, Guid> databases, IFactory<Core.Modules.Manga.IMangaMetadataProvider, Guid> mangaDatabases, SyncplayClient syncplay, IDialogService dialogs, IEnumerable<Descriptor> modules, AnilistPeople people, AnimeSongService songs)
	{
		_people = people;
		_songs = songs;
		_dialogs = dialogs;
		_databases = databases;
		_mangaDatabases = mangaDatabases;
		Playback = playback;
		Syncplay = syncplay;
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
		// The reader's item, in both modes, after the modes' own pages; not registered, so it isn't a startup or agent page.
		var reading = new NavigationItemViewModel
		{
			Label = "Reading",
			Key = "Reading",
			ViewModelType = typeof(Manga.ReaderViewModel),
			AnimatedIcon = () => UI.Icons.ReadingAnimated,
			IsTransient = true,
			IsVisible = false,
		};
		_allItems = [.. navigationItems.Where(x => x.Mode is not null), reading, .. navigationItems.Where(x => x.Mode is null)];
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
		// The mode's kind: Anime and Manga are the first two, in the same order as MediaMode's.
		SearchCategory = (SearchCategory)Mode;

		StartupItem = startup is not null && NavigationItems.Contains(startup) ? startup
			: NavigationItems.FirstOrDefault(x => x.Label == startup?.Label) ?? NavigationItems.FirstOrDefault();
		CurrentItem = StartupItem;

		_preferences = preferences;
		ShowTour = !preferences.ReadKey(TourSeenKey, false);

		_accounts = [.. accounts.Select(x => new AccountViewModel(x))];
		AnimeDatabases = [.. databases.CreateAll().Select(x => new AnimeDatabaseOption(x.Id, x.Name, modules.FirstOrDefault(m => m.Id == x.Id)?.Image ?? ""))];
		ShowAccount(preferences.SelectedAnimeDatabase);
		WeakReferenceMessenger.Default.RegisterAll(this);
	}

	[ObservableProperty]
	public partial List<NavigationItemViewModel> NavigationItems { get; set; }

	/// <summary>Anime or manga: which of the menu's pages show, and what the title bar's search looks for (until another kind is picked).</summary>
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
		SearchCategory = (SearchCategory)mode;
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

	/// <summary>The title bar's network status. Read by the window's binding, so it's created on the UI thread.</summary>
	public UI.Networking.NetworkStatus Network => UI.Networking.NetworkStatus.Current;

	/// <summary>Updating Totoro: a newer version is offered in a bar over the content.</summary>
	public UpdateService Updates { get; }

	[ObservableProperty]
	public partial bool IsUpdateBarOpen { get; set; }

	/// <summary>The account of the selected anime database, shown in the title bar.</summary>
	[ObservableProperty]
	public partial AccountViewModel? Account { get; private set; }

	/// <summary>The anime databases, to switch between from the account flyout (as in Settings).</summary>
	public IReadOnlyList<AnimeDatabaseOption> AnimeDatabases { get; }

	/// <summary>The selected anime database; picking another selects it app-wide.</summary>
	[ObservableProperty]
	public partial AnimeDatabaseOption? AnimeDatabase { get; set; }

	partial void OnAnimeDatabaseChanged(AnimeDatabaseOption? value)
	{
		if (value is not null && value.Id != _preferences.SelectedAnimeDatabase)
		{
			_preferences.SelectedAnimeDatabase = value.Id;
		}
	}

	/// <summary>Watching together in a Syncplay room, joined from the account flyout.</summary>
	public SyncplayClient Syncplay { get; }

	/// <summary>The account flyout's watch-together command: what it does now.</summary>
	public static string WatchTogetherAction(bool isOn) => isOn ? "Leave the room" : "Watch together…";

	/// <summary>Plays the episode the others are watching, from where they are (see <see cref="SyncplayClient.CatchUpText"/>).</summary>
	[RelayCommand]
	private void CatchUp() => _ = Syncplay.CatchUpAsync(); // not waited on: it lasts as long as the episode

	/// <summary>Asks for the room (with the name and server) and joins it; leaves it when in one.</summary>
	[RelayCommand]
	private async Task WatchTogether()
	{
		if (Syncplay.IsOn)
		{
			Syncplay.Leave();
			return;
		}

		DataContainerProperty room = new() { Name = "Room", Description = "Everyone in the same room plays together: pick a name only your friends would guess.", Value = Syncplay.Room };
		DataContainerProperty name = new() { Name = "Name", DisplayName = "Your name", Description = "What the others see.", Value = Syncplay.Name };
		DataContainerProperty server = new() { Name = "Server", DisplayName = "Syncplay server", Description = "host:port; syncplay.pl:8999 is a free public one.", Value = Syncplay.Server };
		DataContainerProperty password = new() { Name = "Password", DisplayName = "Server password", Description = "Only for a private server that has one.", Value = Syncplay.Password, EditorType = DataContainerEditors.Password };
		if (!await _dialogs.EditAsync("Watch together", [room, name, server, password], "Join"))
		{
			return;
		}

		Syncplay.Room = room.Value as string ?? "";
		Syncplay.Name = name.Value as string ?? "";
		Syncplay.Server = server.Value as string ?? "";
		Syncplay.Password = password.Value as string ?? "";
		await Syncplay.JoinAsync();
	}

	/// <summary>What the user's typing to say to the room, from the account flyout.</summary>
	[ObservableProperty]
	public partial string RoomChat { get; set; } = "";

	/// <summary>Says <see cref="RoomChat"/> to the room, and clears it once said.</summary>
	[RelayCommand]
	private void SendRoomChat()
	{
		if (Syncplay.SendChat(RoomChat))
		{
			RoomChat = "";
		}
	}

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

	/// <summary>What the title bar's search looks for: the mode's kind, until another is picked (or typed, e.g. "c:").</summary>
	[ObservableProperty]
	public partial SearchCategory SearchCategory { get; set; }

	/// <summary>The title bar's search: what matches what's typed, of <see cref="SearchCategory"/>.</summary>
	[ObservableProperty]
	public partial List<SearchSuggestion> SearchResults { get; set; } = [];

	// At most.
	private const int SuggestionCount = 10;

	/// <summary>Searches for <see cref="SearchCategory"/> as the user types in the title bar.</summary>
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
			var results = await SearchOrNoneAsync(SearchCategory, query, search.Token);
			if (!search.IsCancellationRequested)
			{
				SearchResults = results;
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	// Offline, or a database is down: nothing found rather than a crash from the title bar.
	private async Task<List<SearchSuggestion>> SearchOrNoneAsync(SearchCategory category, string query, CancellationToken ct)
	{
		try
		{
			return category switch
			{
				SearchCategory.Anime => _databases.CreateDefault() is { } database
					? [.. (await database.SearchAnimeAsync(query)).Take(SuggestionCount).Select(x => SearchSuggestion.From(new MediaSuggestion(x)))]
					: [],
				SearchCategory.Manga => _mangaDatabases.CreateDefault() is { } database
					? [.. (await database.SearchMangaAsync(query, ct)).Take(SuggestionCount).Select(x => SearchSuggestion.From(new MediaSuggestion(x)))]
					: [],
				SearchCategory.Character => [.. (await _people.SearchCharactersAsync(query, SuggestionCount, ct)).Select(x => new SearchSuggestion(
					x.Name, x.Image?.AbsoluteUri, Join("Character", x.Anime.FirstOrDefault()?.Title), "", Lines(x.Name, x.NativeName), x))],
				SearchCategory.VoiceActor => [.. (await _people.SearchVoiceActorsAsync(query, SuggestionCount, ct)).Select(x => new SearchSuggestion(
					x.Name, x.Image?.AbsoluteUri,
					Join("Voice actor", x.Language, x.Roles.FirstOrDefault() is { } role ? $"{role.Character.Name} in {role.Anime.Title}" : null),
					"", Lines(x.Name, x.NativeName), x))],
				_ => [.. (await _songs.SearchSongsAsync(query, SuggestionCount, ct)).Select(x => new SearchSuggestion(
					x.Song.Title, x.Anime.Image, Join(x.Song.Label, x.Song.Artist, x.Anime.Title), "", Lines(x.Song.Title, x.Song.Artist), x))],
			};
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return [];
		}
	}

	private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));

	// For the tooltip: a name, and the native one (or the singer) under it.
	private static string Lines(string first, string second) => second.Length > 0 ? $"{first}\n{second}" : first;

	private void ShowAccount(Guid databaseId)
	{
		// An unset (or unknown) database means the first one, as in the database factory and Settings.
		Account = _accounts.FirstOrDefault(x => x.Id == databaseId) ?? _accounts.FirstOrDefault();
		AnimeDatabase = AnimeDatabases.FirstOrDefault(x => x.Id == databaseId) ?? AnimeDatabases.FirstOrDefault();
	}
}
