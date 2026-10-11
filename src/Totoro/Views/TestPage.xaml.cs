using CommunityToolkit.WinUI;
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

	private void OnPlaygroundSelected(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		if (ViewModel is { } viewModel && sender.SelectedItem?.Tag is string key)
		{
			viewModel.SelectedPlayground = key;
		}
	}
}
