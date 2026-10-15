using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Plugins;

namespace Totoro.ViewModels.Test;

public sealed partial class PluginItemViewModel : ObservableObject
{
	private readonly IPluginManager _pluginManager;

	public PluginItemViewModel(PluginCatalogItem item, IPluginManager pluginManager)
	{
		Item = item;
		_pluginManager = pluginManager;
		IsInstalled = item.IsInstalled;
		HasUpdate = item.HasUpdate;
		InstalledVersion = item.InstalledVersion;
	}

	public PluginCatalogItem Item { get; }
	public Guid Id => Item.Id;
	public string Name => Item.Name;
	public string Version => Item.Version;
	public string Description => Item.Description;
	public string Author => Item.Author;
	public string DownloadUrl => Item.DownloadUrl;
	public string? IconUrl => Item.IconUrl;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(UpdateVisibility))]
	[NotifyPropertyChangedFor(nameof(InstalledVisibility))]
	[NotifyPropertyChangedFor(nameof(NotInstalledVisibility))]
	public partial bool IsInstalled { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(UpdateVisibility))]
	[NotifyPropertyChangedFor(nameof(InstalledVisibility))]
	public partial bool HasUpdate { get; set; }

	[ObservableProperty]
	public partial string? InstalledVersion { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(BusyVisibility))]
	public partial bool IsBusy { get; set; }

	public Microsoft.UI.Xaml.Visibility UpdateVisibility => HasUpdate ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
	public Microsoft.UI.Xaml.Visibility InstalledVisibility => (IsInstalled && !HasUpdate) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
	public Microsoft.UI.Xaml.Visibility NotInstalledVisibility => !IsInstalled ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
	public Microsoft.UI.Xaml.Visibility BusyVisibility => IsBusy ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

	[ObservableProperty]
	public partial double Progress { get; set; }

	[ObservableProperty]
	public partial string StatusText { get; set; } = "";

	[RelayCommand]
	public async Task InstallOrUpdateAsync()
	{
		if (string.IsNullOrWhiteSpace(DownloadUrl)) return;

		IsBusy = true;
		StatusText = HasUpdate ? "Updating..." : "Installing...";

		try
		{
			var progressHandler = new Progress<double>(p => Progress = p * 100);
			await _pluginManager.InstallFromUrlAsync(new Uri(DownloadUrl), progressHandler);
			IsInstalled = true;
			HasUpdate = false;
			InstalledVersion = Version;
			StatusText = "Installed! Restart required.";
		}
		catch (Exception ex)
		{
			StatusText = $"Error: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
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
			IsInstalled = false;
			HasUpdate = false;
			InstalledVersion = null;
			StatusText = "Uninstalled. Restart required.";
		}
		catch (Exception ex)
		{
			StatusText = $"Error: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
		}
	}
}

public partial class PluginsPlaygroundViewModel : ObservableObject
{
	private readonly IPluginLoader _pluginLoader;
	private readonly IPluginManager _pluginManager;
	private readonly IFactory<IAnimeProvider, Guid> _providers;

	public PluginsPlaygroundViewModel(
		IPluginLoader pluginLoader,
		IPluginManager pluginManager,
		IFactory<IAnimeProvider, Guid> providers)
	{
		_pluginLoader = pluginLoader;
		_pluginManager = pluginManager;
		_providers = providers;

		Plugins = [.. _pluginLoader.Plugins];
		DevPaths = new ObservableCollection<string>(_pluginManager.DevPluginPaths);
		AvailableProviders = [.. _providers.CreateAll()];
		SelectedProvider = AvailableProviders.FirstOrDefault();
		RequiresRestart = _pluginManager.RequiresRestart;

		_ = LoadCatalogAsync();
	}

	public IReadOnlyList<PluginInfo> Plugins { get; }
	public ObservableCollection<string> DevPaths { get; }
	public IReadOnlyList<IAnimeProvider> AvailableProviders { get; }
	public ObservableCollection<PluginItemViewModel> CatalogPlugins { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(LoadingVisibility))]
	public partial bool IsLoadingCatalog { get; set; }

	public Microsoft.UI.Xaml.Visibility LoadingVisibility => IsLoadingCatalog ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

	[ObservableProperty]
	public partial bool RequiresRestart { get; set; }

	[ObservableProperty]
	public partial int SelectedTabIndex { get; set; } = 0;

	[ObservableProperty]
	public partial IAnimeProvider? SelectedProvider { get; set; }

	[ObservableProperty]
	public partial string SearchQuery { get; set; } = "Frieren";

	[ObservableProperty]
	public partial string NewDevPath { get; set; } = "";

	[ObservableProperty]
	public partial string StatusMessage { get; set; } = "";

	public ObservableCollection<ProviderShow> Shows { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanFetchEpisodes))]
	public partial ProviderShow? SelectedShow { get; set; }

	public bool CanFetchEpisodes => SelectedShow is not null;

	public ObservableCollection<ProviderEpisode> Episodes { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanFetchServers))]
	public partial ProviderEpisode? SelectedEpisode { get; set; }

	public bool CanFetchServers => SelectedEpisode is not null;

	public ObservableCollection<VideoServer> Servers { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SearchingVisibility))]
	public partial bool IsSearching { get; set; }

	public Microsoft.UI.Xaml.Visibility SearchingVisibility => IsSearching ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

	[RelayCommand]
	public async Task LoadCatalogAsync()
	{
		IsLoadingCatalog = true;
		StatusMessage = "Fetching plugins catalog...";
		CatalogPlugins.Clear();

		try
		{
			var items = await _pluginManager.GetCatalogAsync();
			foreach (var item in items)
			{
				CatalogPlugins.Add(new PluginItemViewModel(item, _pluginManager));
			}
			StatusMessage = $"Found {items.Count} plugins in online catalog.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Failed to load catalog: {ex.Message}";
		}
		finally
		{
			IsLoadingCatalog = false;
			RequiresRestart = _pluginManager.RequiresRestart;
		}
	}

	[RelayCommand]
	public async Task InstallFromFileAsync(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

		IsLoadingCatalog = true;
		StatusMessage = $"Installing plugin from {Path.GetFileName(filePath)}...";

		try
		{
			var installed = await _pluginManager.InstallFromFileAsync(filePath);
			StatusMessage = $"Successfully installed {installed.Name} v{installed.Version}! Restart Totoro to load.";
			RequiresRestart = true;
			await LoadCatalogAsync();
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
	public void RestartApp()
	{
		Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
	}

	[RelayCommand]
	private void AddDevPath()
	{
		if (string.IsNullOrWhiteSpace(NewDevPath)) return;
		_pluginManager.AddDevPluginPath(NewDevPath);
		if (!DevPaths.Contains(NewDevPath))
		{
			DevPaths.Add(NewDevPath);
		}
		NewDevPath = "";
		StatusMessage = "Added dev path. Restart Totoro to load plugins from this directory.";
	}

	[RelayCommand]
	private void RemoveDevPath(string path)
	{
		_pluginManager.RemoveDevPluginPath(path);
		DevPaths.Remove(path);
		StatusMessage = "Removed dev path. Restart Totoro to apply changes.";
	}

	[RelayCommand]
	private async Task SearchShowsAsync()
	{
		if (SelectedProvider is null || string.IsNullOrWhiteSpace(SearchQuery)) return;

		IsSearching = true;
		StatusMessage = $"Searching {SelectedProvider.Name} for \"{SearchQuery}\"...";
		Shows.Clear();
		Episodes.Clear();
		Servers.Clear();

		try
		{
			var results = await SelectedProvider.SearchAsync(SearchQuery, CancellationToken.None);
			foreach (var show in results)
			{
				Shows.Add(show);
			}
			StatusMessage = $"Found {results.Count} shows on {SelectedProvider.Name}.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Search error: {ex.Message}";
		}
		finally
		{
			IsSearching = false;
		}
	}

	[RelayCommand]
	private async Task GetEpisodesAsync()
	{
		if (SelectedProvider is null || SelectedShow is null) return;

		IsSearching = true;
		StatusMessage = $"Fetching episodes for {SelectedShow.Title}...";
		Episodes.Clear();
		Servers.Clear();

		try
		{
			var results = await SelectedProvider.GetEpisodesAsync(SelectedShow.Id, CancellationToken.None);
			foreach (var ep in results)
			{
				Episodes.Add(ep);
			}
			StatusMessage = $"Found {results.Count} episodes.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"GetEpisodes error: {ex.Message}";
		}
		finally
		{
			IsSearching = false;
		}
	}

	[RelayCommand]
	private async Task GetServersAsync()
	{
		if (SelectedProvider is null || SelectedEpisode is null) return;

		IsSearching = true;
		StatusMessage = $"Fetching servers for ep {SelectedEpisode.Number}...";
		Servers.Clear();

		try
		{
			var results = await SelectedProvider.GetServersAsync(SelectedEpisode, CancellationToken.None);
			foreach (var server in results)
			{
				Servers.Add(server);
			}
			StatusMessage = $"Found {results.Count} servers.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"GetServers error: {ex.Message}";
		}
		finally
		{
			IsSearching = false;
		}
	}
}
