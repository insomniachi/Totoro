using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

// ToString is what screen readers announce for each ComboBox item, so it returns the label rather than the record dump.

public sealed record AnimeDatabaseOption(Guid Id, string Name)
{
	public override string ToString() => Name;
}

public sealed record AnimeProviderOption(Guid Id, string Name)
{
	public override string ToString() => Name;
}

public sealed record MediaPlayerOption(Guid Id, string Name)
{
	public override string ToString() => Name;
}

public sealed record ThemeOption(string Label, AppTheme Value)
{
	public override string ToString() => Label;
}

public sealed record BackdropOption(string Label, AppBackdrop Value)
{
	public override string ToString() => Label;
}

public sealed record SkipModeOption(string Label, SkipMode Value)
{
	public override string ToString() => Label;
}

/// <summary>A module in Settings, with a command to edit its settings in a dialog.</summary>
public sealed partial class ModuleSettingsItem(IModuleSettings settings, IDialogService dialogs)
{
	public string Name => settings.Descriptor.Name;

	/// <summary>What there is to edit, e.g. "Include NSFW, Search Limit".</summary>
	public string Summary { get; } = string.Join(", ", settings.Config.ToDataContainer().Select(x => x.DisplayName ?? x.Name));

	[RelayCommand]
	private Task EditAsync() => dialogs.EditSettingsAsync(settings);
}

public partial class SettingsViewModel : ObservableObject
{
	private readonly IPreferences _preferences;
	private readonly IAppearanceService _appearance;

	public SettingsViewModel(
		IEnumerable<NavigationItemViewModel> navigationItems,
		IPreferences preferences,
		IFactory<IAnimeTracker, Guid> trackers,
		IFactory<IAnimeProvider, Guid> providers,
		IFactory<IMediaPlayer, Guid> players,
		IEnumerable<Descriptor> modules,
		IEnumerable<IAccount> accounts,
		IEnumerable<IModuleSettings> moduleSettings,
		IDialogService dialogs,
		IAppearanceService appearance,
		UpdateService updates)
	{
		_preferences = preferences;
		_appearance = appearance;
		Updates = updates;
		Updates.PropertyChanged += (_, _) =>
		{
			OnPropertyChanged(nameof(UpdateStatus));
			OnPropertyChanged(nameof(CanUpdate));
		};
		CheckForUpdates = preferences.CheckForUpdates;

		Theme = Themes.First(x => x.Value == appearance.Theme);
		Backdrop = Backdrops.First(x => x.Value == appearance.Backdrop);

		// Footer items (like Settings itself) aren't offered as a startup page.
		StartupPages = [.. navigationItems.Where(x => !x.IsFooter)];
		StartupPage = preferences.FindStartupItem(StartupPages);

		// Trackers and metadata providers come in pairs per module, so listing the trackers lists the databases.
		AnimeDatabases = [.. trackers.CreateAll().Select(x => new AnimeDatabaseOption(x.Id, x.Name))];
		AnimeDatabase = AnimeDatabases.FirstOrDefault(x => x.Id == preferences.SelectedAnimeDatabase) ?? AnimeDatabases.FirstOrDefault();

		Accounts = [.. accounts.Select(x => new AccountViewModel(x))];
		SyncOtherLists = preferences.SyncOtherLists;

		OpeningSkip = SkipModes.First(x => x.Value == preferences.OpeningSkipMode);
		EndingSkip = SkipModes.First(x => x.Value == preferences.EndingSkipMode);
		// Anime providers come as plugins: on a fresh install there are none (see HasAnimeProviders).
		AnimeProviders = [.. providers.CreateAll().DistinctBy(x => x.Id).Select(x => new AnimeProviderOption(x.Id, x.Name))];
		AnimeProvider = AnimeProviders.FirstOrDefault(x => x.Id == preferences.SelectedAnimeProvider) ?? AnimeProviders.FirstOrDefault();
		// Named as their modules are (the built-in player's own name reads "the built-in player").
		MediaPlayers = [.. players.CreateAll().Select(x => new MediaPlayerOption(x.Id, modules.FirstOrDefault(m => m.Id == x.Id)?.Name ?? x.Name))];
		MediaPlayer = MediaPlayers.FirstOrDefault(x => x.Id == preferences.SelectedMediaPlayer) ?? MediaPlayers.FirstOrDefault();
		PlayNextAutomatically = preferences.PlayNextAutomatically;
		ShowOnDiscord = preferences.ShowOnDiscord;
		ShowNextEpisodeOnCards = preferences.ShowNextEpisodeOnCards;
		ShowNewEpisodesOnCards = preferences.ShowNewEpisodesOnCards;
		ShowAiringStatusOnCards = preferences.ShowAiringStatusOnCards;

		// Only modules with something to edit.
		Modules = [.. moduleSettings.Select(x => new ModuleSettingsItem(x, dialogs)).Where(x => x.Summary.Length > 0)];
	}

	/// <summary>Modules whose settings can be edited (in a dialog).</summary>
	public IReadOnlyList<ModuleSettingsItem> Modules { get; }

	/// <summary>The online services the user can sign in to.</summary>
	public IReadOnlyList<AccountViewModel> Accounts { get; }

	/// <summary>Whether changes to the user's list are made on their other lists too.</summary>
	[ObservableProperty]
	public partial bool SyncOtherLists { get; set; }

	partial void OnSyncOtherListsChanged(bool value) => _preferences.SyncOtherLists = value;

	public IReadOnlyList<AnimeDatabaseOption> AnimeDatabases { get; }

	/// <summary>Selects the module whose tracker and metadata provider the app uses.</summary>
	[ObservableProperty]
	public partial AnimeDatabaseOption? AnimeDatabase { get; set; }

	partial void OnAnimeDatabaseChanged(AnimeDatabaseOption? value)
	{
		if (value is not null && value.Id != _preferences.SelectedAnimeDatabase)
		{
			_preferences.SelectedAnimeDatabase = value.Id;
		}
	}

	public IReadOnlyList<AnimeProviderOption> AnimeProviders { get; }

	/// <summary>Whether any anime provider is installed: they come as plugins.</summary>
	public bool HasAnimeProviders => AnimeProviders.Count > 0;

	/// <summary>The anime provider episodes are found on, unless an anime has its own (its anime settings).</summary>
	[ObservableProperty]
	public partial AnimeProviderOption? AnimeProvider { get; set; }

	partial void OnAnimeProviderChanged(AnimeProviderOption? value)
	{
		if (value is not null && value.Id != _preferences.SelectedAnimeProvider)
		{
			_preferences.SelectedAnimeProvider = value.Id;
		}
	}

	public IReadOnlyList<MediaPlayerOption> MediaPlayers { get; }

	/// <summary>The media player episodes play in.</summary>
	[ObservableProperty]
	public partial MediaPlayerOption? MediaPlayer { get; set; }

	partial void OnMediaPlayerChanged(MediaPlayerOption? value)
	{
		if (value is not null && value.Id != _preferences.SelectedMediaPlayer)
		{
			_preferences.SelectedMediaPlayer = value.Id;
		}
	}

	public IReadOnlyList<SkipModeOption> SkipModes { get; } =
	[
		new("Don't skip", SkipMode.Off),
		new("Offer to skip", SkipMode.Ask),
		new("Skip automatically", SkipMode.Automatic),
	];

	/// <summary>What happens when an opening (or recap) starts.</summary>
	[ObservableProperty]
	public partial SkipModeOption OpeningSkip { get; set; }

	partial void OnOpeningSkipChanged(SkipModeOption value) => _preferences.OpeningSkipMode = value.Value;

	/// <summary>What happens when an ending starts.</summary>
	[ObservableProperty]
	public partial SkipModeOption EndingSkip { get; set; }

	partial void OnEndingSkipChanged(SkipModeOption value) => _preferences.EndingSkipMode = value.Value;

	[ObservableProperty]
	public partial bool PlayNextAutomatically { get; set; }

	partial void OnPlayNextAutomaticallyChanged(bool value) => _preferences.PlayNextAutomatically = value;

	/// <summary>Whether the episode playing shows as the user's activity on Discord.</summary>
	[ObservableProperty]
	public partial bool ShowOnDiscord { get; set; }

	partial void OnShowOnDiscordChanged(bool value) => _preferences.ShowOnDiscord = value;

	/// <summary>Whether cards of anime being watched or planned show how long until the next episode airs.</summary>
	[ObservableProperty]
	public partial bool ShowNextEpisodeOnCards { get; set; }

	partial void OnShowNextEpisodeOnCardsChanged(bool value)
	{
		if (value != _preferences.ShowNextEpisodeOnCards)
		{
			_preferences.ShowNextEpisodeOnCards = value;
		}
	}

	/// <summary>Whether cards of anime being watched show how many aired episodes haven't been watched.</summary>
	[ObservableProperty]
	public partial bool ShowNewEpisodesOnCards { get; set; }

	partial void OnShowNewEpisodesOnCardsChanged(bool value)
	{
		if (value != _preferences.ShowNewEpisodesOnCards)
		{
			_preferences.ShowNewEpisodesOnCards = value;
		}
	}

	/// <summary>Whether cards show a dot when their anime is airing or yet to air.</summary>
	[ObservableProperty]
	public partial bool ShowAiringStatusOnCards { get; set; }

	partial void OnShowAiringStatusOnCardsChanged(bool value)
	{
		if (value != _preferences.ShowAiringStatusOnCards)
		{
			_preferences.ShowAiringStatusOnCards = value;
		}
	}

	/// <summary>Updating Totoro from its GitHub releases.</summary>
	public UpdateService Updates { get; }

	/// <summary>Whether Totoro looks for a newer version when it starts.</summary>
	[ObservableProperty]
	public partial bool CheckForUpdates { get; set; }

	partial void OnCheckForUpdatesChanged(bool value) => _preferences.CheckForUpdates = value;

	/// <summary>The version, and what checking for a newer one found.</summary>
	public string UpdateStatus
	{
		get
		{
			var status = Updates.State switch
			{
				UpdateState.Checking => "Checking for updates…",
				UpdateState.UpToDate => $"Totoro {Updates.CurrentVersion} is the latest version.",
				UpdateState.Available => $"Totoro {Updates.Available?.Version} is available; you have {Updates.CurrentVersion}.",
				UpdateState.Downloading => $"Downloading Totoro {Updates.Available?.Version}: {Updates.Progress:P0}",
				UpdateState.Installing => "Installing it: Totoro closes, and starts again once it's done.",
				UpdateState.Failed => Updates.Error,
				_ => $"Totoro {Updates.CurrentVersion}.",
			};
			return Updates.CanInstall ? status : $"{status} This copy wasn't installed with the installer, so it can't update itself.";
		}
	}

	/// <summary>Whether there's a newer version to install.</summary>
	public bool CanUpdate => Updates.CanInstall && Updates.State is UpdateState.Available or UpdateState.Failed && Updates.Available is not null;

	/// <summary>Opens the episode numbering rules (anime-relations and the user's own).</summary>
	[RelayCommand]
	private Task OpenEpisodeNumberingAsync()
	{
		return Navigators.TryGetNavigator("main", out var main) ? main.NavigateToViewModelAsync(typeof(Relations.AnimeRelationsViewModel)) : Task.CompletedTask;
	}

	/// <summary>Opens the plugins store and management page.</summary>
	[RelayCommand]
	private Task OpenPluginsAsync()
	{
		return Navigators.TryGetNavigator("main", out var main) ? main.NavigateToViewModelAsync(typeof(PluginsViewModel)) : Task.CompletedTask;
	}

	public IReadOnlyList<ThemeOption> Themes { get; } =
	[
		new("Use system setting", AppTheme.System),
		new("Light", AppTheme.Light),
		new("Dark", AppTheme.Dark),
	];

	/// <summary>Applied to the window as soon as it changes.</summary>
	[ObservableProperty]
	public partial ThemeOption Theme { get; set; }

	partial void OnThemeChanged(ThemeOption value)
	{
		if (value.Value != _appearance.Theme)
		{
			_appearance.Theme = value.Value;
		}
	}

	public IReadOnlyList<BackdropOption> Backdrops { get; } =
	[
		new("Mica", AppBackdrop.Mica),
		new("Mica Alt", AppBackdrop.MicaAlt),
		new("Acrylic", AppBackdrop.Acrylic),
		new("None", AppBackdrop.None),
	];

	/// <summary>Applied to the window as soon as it changes.</summary>
	[ObservableProperty]
	public partial BackdropOption Backdrop { get; set; }

	partial void OnBackdropChanged(BackdropOption value)
	{
		if (value.Value != _appearance.Backdrop)
		{
			_appearance.Backdrop = value.Value;
		}
	}

	public IReadOnlyList<NavigationItemViewModel> StartupPages { get; }

	[ObservableProperty]
	public partial NavigationItemViewModel? StartupPage { get; set; }

	partial void OnStartupPageChanged(NavigationItemViewModel? value)
	{
		if (value is not null && value.Label != _preferences.StartupPage)
		{
			_preferences.StartupPage = value.Label;
		}
	}
}
