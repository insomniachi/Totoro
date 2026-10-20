using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.Playback;
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

	private void OnSectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		var library = sender.SelectedItem == LibrarySection;
		LibraryPanel.Visibility = library ? Visibility.Visible : Visibility.Collapsed;
		SessionPanel.Visibility = library ? Visibility.Collapsed : Visibility.Visible;
	}

	private void OnEditInfoClick(object sender, RoutedEventArgs e) => _ = ViewModel?.EditInfoAsync((DownloadedAnime)((FrameworkElement)sender).Tag);

	private void OnDeleteClick(object sender, RoutedEventArgs e)
	{
		// The confirmation flyout the button's in: its anime is about to go from the list.
		foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
		{
			popup.IsOpen = false;
		}

		ViewModel?.Delete((DownloadedAnime)((FrameworkElement)sender).Tag);
	}

	public static Visibility VisibleIfNone(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

	public static bool IsUnknown(double progress) => progress < 0;

	public static double Percent(double progress) => Math.Max(0, progress) * 100;
}
