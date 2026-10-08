using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.UI.Navigation;
using Totoro.ViewModels;


namespace Totoro.Views;

public sealed partial class Home : Page
{
	public Home()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial HomeViewModel? ViewModel { get; set; }

	private void Button_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
	{
		Navigators.GetNavigator("pane").NavigateToViewModel(typeof(HomeViewModel));
	}
}
