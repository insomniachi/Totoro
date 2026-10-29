using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Manga;
using Totoro.ViewModels;

namespace Totoro.Views;

/// <summary>The user's list, anime or manga: what both Library pages show (see <see cref="MediaListViewModel"/>).</summary>
public sealed partial class MediaListView : UserControl
{
	public MediaListView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MediaListViewModel? ViewModel { get; set; }

	/// <summary>Number of shimmer cards shown while the list loads; enough to fill a large window.</summary>
	public IReadOnlyList<int> PlaceholderCards { get; } = [.. Enumerable.Range(0, 12)];

	private void OnListViewChanged(ScrollView sender, object args)
	{
		// Offer a way back once the top of the list is more than a screen away.
		GoToTopButton.Visibility = sender.VerticalOffset > sender.ViewportHeight ? Visibility.Visible : Visibility.Collapsed;
	}

	private void OnGoToTopClick(object sender, RoutedEventArgs e)
	{
		ListScrollView.ScrollTo(0, 0);
	}
}

/// <summary>A list's card template for its items' kind: the anime one, or the manga one.</summary>
public sealed partial class MediaTemplateSelector : DataTemplateSelector
{
	public DataTemplate? Anime { get; set; }

	public DataTemplate? Manga { get; set; }

	protected override DataTemplate? SelectTemplateCore(object item) => item is MangaModel ? Manga : Anime;

	protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
