using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

// ToString is what screen readers announce for each ComboBox item, so it returns the label rather than the record dump.

public sealed record AnimeDatabaseOption(Guid Id, string Name)
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
		IFactory<IMediaPlayer, Guid> players,
		IEnumerable<Descriptor> modules,
		IEnumerable<IAccount> accounts,
		IEnumerable<IModuleSettings> moduleSettings,
		IDialogService dialogs,
		IAppearanceService appearance)
	{
		_preferences = preferences;
		_appearance = appearance;

		Theme = Themes.First(x => x.Value == appearance.Theme);
		Backdrop = Backdrops.First(x => x.Value == appearance.Backdrop);

		// Footer items (like Settings itself) aren't offered as a startup page.
		StartupPages = [.. navigationItems.Where(x => !x.IsFooter)];
		StartupPage = preferences.FindStartupItem(StartupPages);

		// Trackers and metadata providers come in pairs per module, so listing the trackers lists the databases.
		AnimeDatabases = [.. trackers.CreateAll().Select(x => new AnimeDatabaseOption(x.Id, x.Name))];
		AnimeDatabase = AnimeDatabases.FirstOrDefault(x => x.Id == preferences.SelectedAnimeDatabase) ?? AnimeDatabases.FirstOrDefault();

		Accounts = [.. accounts.Select(x => new AccountViewModel(x))];

		OpeningSkip = SkipModes.First(x => x.Value == preferences.OpeningSkipMode);
		EndingSkip = SkipModes.First(x => x.Value == preferences.EndingSkipMode);
		// Named as their modules are (the built-in player's own name reads "the built-in player").
		MediaPlayers = [.. players.CreateAll().Select(x => new MediaPlayerOption(x.Id, modules.FirstOrDefault(m => m.Id == x.Id)?.Name ?? x.Name))];
		MediaPlayer = MediaPlayers.FirstOrDefault(x => x.Id == preferences.SelectedMediaPlayer) ?? MediaPlayers.FirstOrDefault();
		PlayNextAutomatically = preferences.PlayNextAutomatically;

		// Only modules with something to edit.
		Modules = [.. moduleSettings.Select(x => new ModuleSettingsItem(x, dialogs)).Where(x => x.Summary.Length > 0)];
	}

	/// <summary>Modules whose settings can be edited (in a dialog).</summary>
	public IReadOnlyList<ModuleSettingsItem> Modules { get; }

	/// <summary>The online services the user can sign in to.</summary>
	public IReadOnlyList<AccountViewModel> Accounts { get; }

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

	/// <summary>Opens the episode numbering rules (anime-relations and the user's own).</summary>
	[RelayCommand]
	private Task OpenEpisodeNumberingAsync()
	{
		return Navigators.TryGetNavigator("main", out var main) ? main.NavigateToViewModelAsync(typeof(Relations.AnimeRelationsViewModel)) : Task.CompletedTask;
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
