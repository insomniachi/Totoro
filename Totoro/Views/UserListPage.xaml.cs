using CommunityToolkit.WinUI;
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
}
