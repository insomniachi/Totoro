using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Manga;

namespace Totoro.Views.Manga;

public sealed partial class MangaDiscoverPage : Page
{
	public MangaDiscoverPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MangaDiscoverViewModel? ViewModel { get; set; }

	// Rows as the anime Discover page's: paged by their header's buttons, sliding in quickly.
	private void OnPreviousPage(object sender, RoutedEventArgs e) => DiscoverPage.ScrollRow(sender, -1);

	private void OnNextPage(object sender, RoutedEventArgs e) => DiscoverPage.ScrollRow(sender, 1);

	private void OnRowScrollAnimationStarting(ScrollView sender, ScrollingScrollAnimationStartingEventArgs args) => DiscoverPage.SlideRow(args);

	private void OnRowChanged(object sender, object e)
	{
		if (sender == TrendingRow)
		{
			DiscoverPage.UpdateRowButtons(TrendingRow, TrendingPrevious, TrendingNext);
		}
		else
		{
			DiscoverPage.UpdateRowButtons(PopularRow, PopularPrevious, PopularNext);
		}
	}
}
