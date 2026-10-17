using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Plugins;

namespace Totoro.ViewModels;

public sealed partial class PluginItemViewModel : ObservableObject
{
	private readonly IPluginManager _pluginManager;

	public PluginItemViewModel(PluginCatalogItem item, IPluginManager pluginManager)
	{
		Item = item;
		_pluginManager = pluginManager;
		Refresh();
	}

	public PluginCatalogItem Item { get; }
	public Guid Id => Item.Id;
	public string Name => Item.Name;
	public string Version => Item.Version;
	public string Description => Item.Description;
	public string Author => Item.Author;
	public string DownloadUrl => Item.DownloadUrl;
	public string? IconUrl => Item.IconUrl;
	public bool IsDev => Item.IsDev;
	public bool IsCompatible => Item.IsCompatible;

	/// <summary>Why it can't be installed, for a plugin needing a later version of Totoro.</summary>
	public string IncompatibleText => IsCompatible ? "" : $"Needs Totoro {Item.MinAppVersion}";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsUpdate), nameof(ShowsInstalled), nameof(ShowsNotInstalled))]
	public partial bool IsInstalled { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsUpdate), nameof(ShowsInstalled))]
	public partial bool HasUpdate { get; set; }

	[ObservableProperty]
	public partial string InstalledVersion { get; set; } = "";

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	// Which of the card's states shows.
	public bool ShowsUpdate => IsInstalled && HasUpdate;
	public bool ShowsInstalled => IsInstalled && !HasUpdate;
	public bool ShowsNotInstalled => !IsInstalled;

	/// <summary>Dev plugins are loaded from their own folders: removed there, not here.</summary>
	public bool CanUninstall => !IsDev;

	[ObservableProperty]
	public partial double Progress { get; set; }

	[ObservableProperty]
	public partial string StatusText { get; set; } = "";

	/// <summary>Shows the plugin's state as the plugin manager has it, e.g. after an agent installed it.</summary>
	public void Refresh()
	{
		_pluginManager.Annotate(Item);
		IsInstalled = Item.IsInstalled;
		HasUpdate = Item.HasUpdate;
		InstalledVersion = Item.InstalledVersion ?? "";
		if (!IsBusy && Item.LoadError is { } error)
		{
			StatusText = $"Didn't load: {error}";
		}
	}

	[RelayCommand]
	public async Task InstallOrUpdateAsync()
	{
		if (string.IsNullOrWhiteSpace(DownloadUrl)) return;

		IsBusy = true;
		StatusText = HasUpdate ? "Updating..." : "Installing...";

		try
		{
			var progressHandler = new Progress<double>(p => Progress = p * 100);
			await _pluginManager.InstallAsync(Item, progressHandler);
			StatusText = "Installed! Restart required.";
		}
		catch (Exception ex)
		{
			StatusText = $"Error: {ex.Message}";
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
		StatusText = "Uninstalling...";

		try
		{
			await _pluginManager.UninstallAsync(Id);
			StatusText = "Uninstalled. Restart required.";
		}
		catch (Exception ex)
		{
			StatusText = $"Error: {ex.Message}";
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
	private readonly List<PluginItemViewModel> _allCatalogItems = [];

	public PluginsViewModel(
		IPluginLoader pluginLoader,
		IPluginManager pluginManager)
	{
		_pluginLoader = pluginLoader;
		_pluginManager = pluginManager;

		InstalledPlugins = [.. _pluginLoader.Plugins];
		RequiresRestart = _pluginManager.RequiresRestart;
		// Installs and uninstalls from anywhere (an item here, or an agent).
		WeakReferenceMessenger.Default.Register(this);

		_ = LoadCatalogAsync();
	}

	public IReadOnlyList<PluginInfo> InstalledPlugins { get; private set; }
	public ObservableCollection<PluginItemViewModel> CatalogPlugins { get; } = [];

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
		StatusMessage = "Fetching plugins catalog...";
		CatalogPlugins.Clear();
		_allCatalogItems.Clear();

		try
		{
			var items = await _pluginManager.GetCatalogAsync();
			foreach (var item in items)
			{
				var vm = new PluginItemViewModel(item, _pluginManager);
				_allCatalogItems.Add(vm);
			}
			ApplyFilter();
			StatusMessage = $"Found {items.Count} plugin(s) in catalog.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Failed to load catalog: {ex.Message}";
		}
		finally
		{
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
		StatusMessage = $"Installing {Path.GetFileName(filePath)}...";

		try
		{
			var installed = await _pluginManager.InstallFromFileAsync(filePath);
			await LoadCatalogAsync();
			StatusMessage = $"Successfully installed {installed.Name} v{installed.Version}! Restart Totoro to load.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Failed to install package: {ex.Message}";
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
		CatalogPlugins.Clear();
		var filter = SearchFilter.Trim();
		foreach (var item in _allCatalogItems)
		{
			if (string.IsNullOrWhiteSpace(filter) ||
			    item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
			    item.Author.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
			    item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
			{
				CatalogPlugins.Add(item);
			}
		}
	}
}
