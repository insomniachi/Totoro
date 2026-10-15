using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
