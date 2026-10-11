using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
			ViewModel?.RemoveCommand.Execute(item);
		}
	}
}
