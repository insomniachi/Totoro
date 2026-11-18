using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Plugins;
using Totoro.ViewModels;

namespace Totoro.Setup;

/// <summary>A setup step listing the catalog's plugins of one module type, each to download.</summary>
public sealed partial class PluginStep : UserControl
{
	private readonly List<Task> _installs = [];

	public PluginStep()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial string? Title { get; set; }

	[GeneratedDependencyProperty]
	public partial string? Description { get; set; }

	/// <summary>The downloads going on, which have to finish before the plugins are loaded.</summary>
	public Task Installing => Task.WhenAll(_installs);

	/// <param name="moduleType">As <see cref="PluginItemViewModel.ModuleType"/> has it, e.g. "AnimeProvider".</param>
	/// <param name="builtIn">Modules of that type that ship with Totoro, listed first with nothing to download.</param>
	public async Task LoadAsync(IPluginManager plugins, string moduleType, string[]? builtIn = null)
	{
		var items = (builtIn ?? []).Select(name => new PluginItemViewModel(
			new PluginCatalogItem { Id = Guid.Empty, Name = name, Version = "", DownloadUrl = "" }, plugins, isBuiltIn: true)).ToList();
		try
		{
			items.AddRange((await plugins.GetCatalogAsync())
				.Where(x => x.IsCompatible)
				.Select(x => new PluginItemViewModel(x, plugins))
				.Where(x => x.ModuleType == moduleType));
			if (plugins.CatalogError is not null)
			{
				ShowNote("Couldn't load plugins. You can add them later from the Plugins page.");
			}
		}
		catch (Exception)
		{
			ShowNote("Couldn't load plugins. You can add them later from the Plugins page.");
		}
		finally
		{
			LoadingRing.IsActive = false;
			LoadingRing.Visibility = Visibility.Collapsed;
			List.ItemsSource = items;
		}
	}

	/// <summary>The app's services are built: a plugin installed now would only load after a restart.</summary>
	public void Lock()
	{
		List.IsEnabled = false;
		ShowNote("To add more plugins, use the Plugins page once setup is done.");
	}

	private void ShowNote(string text)
	{
		Note.Message = text;
		Note.IsOpen = true;
	}

	private async void OnDownload(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is not PluginItemViewModel item)
		{
			return;
		}

		var install = item.InstallOrUpdateAsync();
		_installs.Add(install);
		await install;
		// It loads when setup goes on, not after a restart as the Plugins page says.
		if (!item.HasError)
		{
			item.StatusText = "";
		}
	}

	public static string DownloadName(string name) => $"Download {name}";

	public static bool Not(bool value) => !value;

	public static string State(bool isBuiltIn, bool isInstalled, string status) =>
		status.Length > 0 ? status : isBuiltIn ? "Built in" : isInstalled ? "Installed" : "";
}
