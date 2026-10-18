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

	private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		if (ViewModel is null)
		{
			return;
		}

		// The two-way binding passes the text on only when the box loses focus: Enter would search for the old text.
		ViewModel.Query = args.QueryText;
		ViewModel.SearchCommand.Execute(null);
	}
}
