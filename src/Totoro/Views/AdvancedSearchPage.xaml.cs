using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class AdvancedSearchPage : Page
{
	public AdvancedSearchPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AdvancedSearchViewModel? ViewModel { get; set; }
}
