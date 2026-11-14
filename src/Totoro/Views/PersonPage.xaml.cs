using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.UI.Navigation;
using Totoro.ViewModels;

namespace Totoro.Views;

/// <summary>A character's or voice actor's page, in the side pane.</summary>
public sealed partial class PersonPage : Page
{
	public PersonPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial PersonViewModel? ViewModel { get; set; }

	// Their page replaces this one in the side pane; Back returns here.
	private async void OnPersonClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: { } target } && Navigators.TryGetNavigator("pane", out var pane))
		{
			await pane.NavigateToDataAsync(target);
		}
	}
}
