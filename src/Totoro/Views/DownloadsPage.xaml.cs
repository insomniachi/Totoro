using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class DownloadsPage : Page
{
	public DownloadsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial DownloadsViewModel? ViewModel { get; set; }

	public static Visibility VisibleIfNone(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

	public static bool IsUnknown(double progress) => progress < 0;

	public static double Percent(double progress) => Math.Max(0, progress) * 100;

	private async void OnChangeFolderClick(object sender, RoutedEventArgs e)
	{
		var picker = new FolderPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
		if (await picker.PickSingleFolderAsync() is { } folder)
		{
			ViewModel?.SetFolder(folder.Path);
		}
	}

	private async void OnBrowseYtDlpClick(object sender, RoutedEventArgs e)
	{
		var picker = new FileOpenPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
		picker.FileTypeFilter.Add(".exe");
		if (await picker.PickSingleFileAsync() is { } file && ViewModel is not null)
		{
			ViewModel.YtDlpPath = file.Path;
		}
	}
}
