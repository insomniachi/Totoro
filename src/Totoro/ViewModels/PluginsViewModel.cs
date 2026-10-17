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

	/// <summary>The card's version line, e.g. "Version 1.0.0 installed · 1.1.0 available".</summary>
	public string VersionText
	{
		get
		{
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
	[NotifyPropertyChangedFor(nameof(ShowsUpdate), nameof(ShowsInstalled), nameof(ShowsNotInstalled), nameof(ShowsUninstall), nameof(VersionText))]
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
	public bool ShowsUninstall => IsInstalled && !IsDev;

	[ObservableProperty]
	public partial double Progress { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsStatus))]
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
		StatusMessage = "";
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
		}
		catch (Exception ex)
		{
			StatusMessage = $"Couldn't load the catalog: {ex.Message}";
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
