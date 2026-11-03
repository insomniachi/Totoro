using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class ProviderWatchPage : Page
{
	public ProviderWatchPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial ProviderWatchViewModel? ViewModel { get; set; }

	private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => ViewModel?.SearchCommand.Execute(null);
}
