using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Totoro.ViewModels;

namespace Totoro.Views.Settings;

public sealed partial class DownloadsSettingsPage : Page
{
	public DownloadsSettingsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SettingsViewModel? ViewModel { get; set; }

	private async void OnChangeDownloadFolderClick(object sender, RoutedEventArgs e)
	{
		var picker = new FolderPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
		if (await picker.PickSingleFolderAsync() is { } folder)
		{
			ViewModel?.SetDownloadFolder(folder.Path);
		}
	}

	private async void OnBrowseYtDlpClick(object sender, RoutedEventArgs e)
	{
		if (await PickExeAsync() is { } path && ViewModel is not null)
		{
			ViewModel.YtDlpPath = path;
		}
	}

	private async Task<string?> PickExeAsync()
	{
		var picker = new FileOpenPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
		picker.FileTypeFilter.Add(".exe");
		return (await picker.PickSingleFileAsync())?.Path;
	}
}
