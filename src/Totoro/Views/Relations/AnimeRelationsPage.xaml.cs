using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.ViewModels.Relations;

namespace Totoro.Views.Relations;

public sealed partial class AnimeRelationsPage : Page
{
	public AnimeRelationsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AnimeRelationsViewModel? ViewModel { get; set; }

	// The rules' buttons are in a template, where x:Bind can't reach the page's view model.

	private void OnEditClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: UserRuleItem item })
		{
			ViewModel?.EditCommand.Execute(item);
		}
	}

	private void OnRemoveClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: UserRuleItem item })
		{
			// The confirmation flyout the button's in: its rule is about to go from the list.
			foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
			{
				popup.IsOpen = false;
			}

			ViewModel?.RemoveCommand.Execute(item);
		}
	}
}
