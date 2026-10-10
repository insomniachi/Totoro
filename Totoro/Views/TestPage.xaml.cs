using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class TestPage : Page
{
	public TestPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial TestViewModel? ViewModel { get; set; }

	private void OnResetClick(object sender, RoutedEventArgs e)
	{
		ViewModel?.Html = TestViewModel.Sample;
	}
}
