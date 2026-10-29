using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Chat;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Plugins;
using Totoro.Core.Settings;

namespace Totoro.ViewModels;

public sealed partial class PluginItemViewModel : ObservableObject
{
	private readonly IPluginManager _pluginManager;

	public PluginItemViewModel(PluginCatalogItem item, IPluginManager pluginManager, ModuleSettingsItem? settings = null, bool isBuiltIn = false)
	{
		Item = item;
		_pluginManager = pluginManager;
		Settings = settings;
		IsBuiltIn = isBuiltIn;
		Refresh();
	}

	public PluginCatalogItem Item { get; }

	/// <summary>A module that ships with Totoro: always installed, and not uninstalled or updated on its own.</summary>
	public bool IsBuiltIn { get; }

	/// <summary>Its first module type (e.g. "AnimeProvider"), which the page groups it under.</summary>
	// The catalog names the interface ("IAnimeProvider"), manifests the type ("AnimeProvider").
	public string ModuleType => Item.ModuleTypes.FirstOrDefault() is { } type
		? type is ['I', var c, ..] && char.IsUpper(c) ? type[1..] : type
		: "";

	/// <summary>The loaded plugin's settings, if it has any to edit.</summary>
	public ModuleSettingsItem? Settings { get; }
	public Guid Id => Item.Id;
	public string Name => Item.Name;
	public string Version => Item.Version;
	public string Description => Item.Description;
	public string Author => Item.Author;
	public string DownloadUrl => Item.DownloadUrl;
	/// <summary>The plugin's own icon when it's loaded (no download, and dev plugins aren't in the catalog), else the catalog's.</summary>
	public string Icon => Item.LocalIcon ?? Item.IconUrl ?? "";
	public bool IsDev => Item.IsDev;
	public bool IsCompatible => Item.IsCompatible;

	/// <summary>Why it can't be installed, for a plugin needing a later version of Totoro.</summary>
	public string IncompatibleText => IsCompatible ? "" : $"Needs Totoro {Item.MinAppVersion}";

	/// <summary>The card's version line, e.g. "Version 1.0.0 installed · 1.1.0 available".</summary>
	public string VersionText
	{
		get
		{
			if (IsBuiltIn) return $"Version {Version} · Built in";

			var text = !IsInstalled ? $"Version {Version}"
				: HasUpdate ? $"Version {InstalledVersion} installed · {Version} available"
				: $"Version {InstalledVersion} installed";
			if (IsDev)
			{
				text += " · Dev plugin";
			}

			return IsCompatible ? text : $"{text} · {IncompatibleText}";
		}
	}

	/// <summary>Whether <see cref="StatusText"/> is an error.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsStatus))]
	public partial bool HasError { get; set; }

	/// <summary>Whether there's a status that isn't an error (those show in red).</summary>
	public bool ShowsStatus => !HasError && StatusText.Length > 0;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsUpdate), nameof(ShowsInstalled), nameof(ShowsNotInstalled), nameof(ShowsUninstall), nameof(ShowsSettings), nameof(VersionText))]
	public partial bool IsInstalled { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsUpdate), nameof(ShowsInstalled), nameof(VersionText))]
	public partial bool HasUpdate { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(VersionText))]
	public partial string InstalledVersion { get; set; } = "";

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	// Which of the card's states shows.
	public bool ShowsUpdate => IsInstalled && HasUpdate;
	public bool ShowsInstalled => IsInstalled && !HasUpdate;
	public bool ShowsNotInstalled => !IsInstalled;

	/// <summary>Installed, and not a dev plugin: those are loaded from their own folders, and removed there.</summary>
	public bool ShowsUninstall => IsInstalled && !IsDev && !IsBuiltIn;

	public bool ShowsSettings => IsInstalled && Settings is not null;

	[ObservableProperty]
	public partial double Progress { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsStatus))]
	public partial string StatusText { get; set; } = "";

	/// <summary>Shows the plugin's state as the plugin manager has it, e.g. after an agent installed it.</summary>
	public void Refresh()
	{
		if (IsBuiltIn)
		{
			IsInstalled = true;
			return;
		}

		_pluginManager.Annotate(Item);
		IsInstalled = Item.IsInstalled;
		HasUpdate = Item.HasUpdate;
		InstalledVersion = Item.InstalledVersion ?? "";
		if (!IsBusy && Item.LoadError is { } error)
		{
			StatusText = $"Didn't load: {error}";
			HasError = true;
		}
	}

	[RelayCommand]
	public async Task InstallOrUpdateAsync()
	{
		if (string.IsNullOrWhiteSpace(DownloadUrl)) return;

		IsBusy = true;
		HasError = false;
		StatusText = HasUpdate ? "Updating…" : "Installing…";

		try
		{
			var progressHandler = new Progress<double>(p => Progress = p * 100);
			await _pluginManager.InstallAsync(Item, progressHandler);
			StatusText = "Installed. Restart Totoro to use it.";
		}
		catch (Exception ex)
		{
			StatusText = ex.Message;
			HasError = true;
		}
		finally
		{
			IsBusy = false;
			Refresh();
		}
	}

	[RelayCommand]
	public async Task UninstallAsync()
	{
		IsBusy = true;
		HasError = false;
		StatusText = "Uninstalling…";

		try
		{
			await _pluginManager.UninstallAsync(Id);
			StatusText = "Uninstalled. Restart Totoro to finish.";
		}
		catch (Exception ex)
		{
			StatusText = ex.Message;
			HasError = true;
		}
		finally
		{
			IsBusy = false;
			Refresh();
		}
	}
}

public partial class PluginsViewModel : ObservableObject, IRecipient<PluginsChangedMessage>
{
	private readonly IPluginLoader _pluginLoader;
	private readonly IPluginManager _pluginManager;
	private readonly Dictionary<Guid, ModuleSettingsItem> _settings;
	private readonly List<PluginItemViewModel> _allCatalogItems = [];
	private readonly List<PluginItemViewModel> _builtInItems;

	// The type filters, in the order they show (and "All" lists them in); a type not here goes under Other.
	private static readonly (string Type, string Title)[] s_groups =
	[
		("AnimeProvider", "Anime providers"),
		("TorrentStreamer", "Torrent streamers"),
		("MediaPlayer", "Media players"),
		("AnimeDatabase", "Anime databases"),
		("ChatModel", "Chat models"),
	];

	public PluginsViewModel(
		IPluginLoader pluginLoader,
		IPluginManager pluginManager,
		IEnumerable<IModuleSettings> moduleSettings,
		IEnumerable<Descriptor> modules,
		IServiceProviderIsKeyedService services,
		IDialogService dialogs)
	{
		_pluginLoader = pluginLoader;
		_pluginManager = pluginManager;
		// A plugin's id is its module's (the loader gives the module's descriptor the manifest's id).
		_settings = moduleSettings
			.Select(x => new ModuleSettingsItem(x, dialogs))
			.Where(x => x.Summary.Length > 0)
			.ToDictionary(x => x.Id);

		// The modules that aren't plugins, typed by what they register (they have no manifest to say).
		string TypeOf(Guid id) =>
			services.IsKeyedService(typeof(IAnimeProvider), id) ? "AnimeProvider"
			: services.IsKeyedService(typeof(ITorrentStreamer), id) ? "TorrentStreamer"
			: services.IsKeyedService(typeof(IMediaPlayer), id) ? "MediaPlayer"
			: services.IsKeyedService(typeof(IAnimeTracker), id) ? "AnimeDatabase"
			: services.IsKeyedService(typeof(Core.Modules.Manga.IMangaProvider), id) ? "MangaProvider"
			: services.IsKeyedService(typeof(Core.Modules.Manga.IMangaTracker), id) ? "MangaDatabase"
			: services.IsKeyedService(typeof(IChatModel), id) ? "ChatModel"
			: "";
		var pluginIds = _pluginLoader.Plugins.Select(x => x.Id).ToHashSet();
		_builtInItems = [.. modules
			.Where(x => !pluginIds.Contains(x.Id))
			.DistinctBy(x => x.Id)
			.Select(x => new PluginItemViewModel(
				new PluginCatalogItem
				{
					Id = x.Id,
					Name = x.Name,
					Version = x.Version.ToString(3),
					Description = x.Description ?? "",
					DownloadUrl = "",
					ModuleTypes = [TypeOf(x.Id)],
					LocalIcon = x.Image,
				},
				_pluginManager,
				_settings.GetValueOrDefault(x.Id),
				isBuiltIn: true))];

		InstalledPlugins = [.. _pluginLoader.Plugins];
		RequiresRestart = _pluginManager.RequiresRestart;
		// Installs and uninstalls from anywhere (an item here, or an agent).
		WeakReferenceMessenger.Default.Register(this);

		_ = LoadCatalogAsync();
	}

	public IReadOnlyList<PluginInfo> InstalledPlugins { get; private set; }
	/// <summary>The catalog's plugins and the built-in modules that match the search and type filter, by module type.</summary>
	public ObservableCollection<PluginItemViewModel> Plugins { get; } = [];

	/// <summary>The pills above the list: All, then a module type each.</summary>
	public IReadOnlyList<string> TypeFilters { get; } = ["All", .. s_groups.Select(x => x.Title), "Other"];

	/// <summary>The selected pill; 0 is All.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsTestPlugins))]
	public partial int SelectedTypeIndex { get; set; }

	/// <summary>The plugin tester only tests anime providers, so it's offered on their pill.</summary>
	public bool ShowsTestPlugins => SelectedTypeIndex == 1;

	partial void OnSelectedTypeIndexChanged(int value) => ApplyFilter();

	[ObservableProperty]
	public partial bool IsLoadingCatalog { get; set; }

	[ObservableProperty]
	public partial bool RequiresRestart { get; set; }

	[ObservableProperty]
	public partial string SearchFilter { get; set; } = "";

	partial void OnSearchFilterChanged(string value) => ApplyFilter();

	[ObservableProperty]
	public partial string StatusMessage { get; set; } = "";

	public void Receive(PluginsChangedMessage message)
	{
		RequiresRestart = _pluginManager.RequiresRestart;
		foreach (var item in _allCatalogItems)
		{
			item.Refresh();
		}
	}

	[RelayCommand]
	public async Task LoadCatalogAsync()
	{
		IsLoadingCatalog = true;
		StatusMessage = "";
		_allCatalogItems.Clear();
		// The built-ins show while the catalog loads.
		ApplyFilter();

		try
		{
			var items = await _pluginManager.GetCatalogAsync();
			foreach (var item in items)
			{
				var vm = new PluginItemViewModel(item, _pluginManager, _settings.GetValueOrDefault(item.Id));
				_allCatalogItems.Add(vm);
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"Couldn't load the catalog: {ex.Message}";
		}
		finally
		{
			// Built-ins show even when the catalog doesn't load.
			ApplyFilter();
			IsLoadingCatalog = false;
			RequiresRestart = _pluginManager.RequiresRestart;
			InstalledPlugins = [.. _pluginLoader.Plugins];
		}
	}

	[RelayCommand]
	public async Task InstallFromFileAsync(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

		IsLoadingCatalog = true;
		StatusMessage = "";

		try
		{
			await _pluginManager.InstallFromFileAsync(filePath);
			await LoadCatalogAsync();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Couldn't install {Path.GetFileName(filePath)}: {ex.Message}";
		}
		finally
		{
			IsLoadingCatalog = false;
		}
	}

	[RelayCommand]
	public void RestartApp() => App.Restart();

	[RelayCommand]
	public async Task TestPluginsAsync()
	{
		if (Totoro.UI.Navigation.Navigators.TryGetNavigator("main", out var main))
		{
			await main.NavigateToViewModelAsync(typeof(PluginTesterViewModel));
		}
	}

	private void ApplyFilter()
	{
		Plugins.Clear();
		var filter = SearchFilter.Trim();
		var matches = _allCatalogItems.Concat(_builtInItems).Where(item =>
			(SelectedTypeIndex <= 0 || TypeIndex(item) == SelectedTypeIndex - 1) &&
			(string.IsNullOrWhiteSpace(filter) ||
			 item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
			 item.Author.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
			 item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)));

		foreach (var item in matches.OrderBy(TypeIndex))
		{
			Plugins.Add(item);
		}

		static int TypeIndex(PluginItemViewModel item) =>
			Array.FindIndex(s_groups, g => g.Type == item.ModuleType) is var i and >= 0 ? i : s_groups.Length;
	}
}
