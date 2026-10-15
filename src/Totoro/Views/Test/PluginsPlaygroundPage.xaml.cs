using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Test;

namespace Totoro.Views.Test;

public sealed partial class PluginsPlaygroundPage : Page
{
	public PluginsPlaygroundPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial PluginsPlaygroundViewModel? ViewModel { get; set; }

	private void OnTabSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		if (sender.SelectedItem is SelectorBarItem item && int.TryParse(item.Tag?.ToString(), out var index))
		{
			CatalogSection.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
			InstalledSection.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
			PlaygroundSection.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
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

	private void OnRemoveDevPathClicked(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: string path } && ViewModel is not null)
		{
			ViewModel.RemoveDevPathCommand.Execute(path);
		}
	}
}

