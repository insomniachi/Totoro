using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class PluginsPage : Page
{
	public PluginsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial PluginsViewModel? ViewModel { get; set; }

	// The confirm button runs the command; this closes the flyout it's in.
	private void OnUninstallClick(object sender, RoutedEventArgs e)
	{
		foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
		{
			popup.IsOpen = false;
		}
	}

	private async void OnBrowsePluginPackageClicked(object sender, RoutedEventArgs e)
	{
		if (ViewModel is null) return;

		var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(this.XamlRoot.ContentIslandEnvironment.AppWindowId);
		picker.FileTypeFilter.Add(".totoro-plugin");
		picker.FileTypeFilter.Add(".zip");
		var file = await picker.PickSingleFileAsync();
		if (file is not null)
		{
			await ViewModel.InstallFromFileCommand.ExecuteAsync(file.Path);
		}
	}
}
