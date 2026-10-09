using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;


namespace Totoro.Views;

public sealed partial class UserListPage : Page
{
	public UserListPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial UserListViewModel? ViewModel { get; set; }

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
