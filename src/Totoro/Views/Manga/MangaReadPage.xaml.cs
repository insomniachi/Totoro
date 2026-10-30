using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Manga;

namespace Totoro.Views.Manga;

public sealed partial class MangaReadPage : Page
{
	public MangaReadPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MangaReadViewModel? ViewModel { get; set; }

	private void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		if (ViewModel is null)
		{
			return;
		}

		// The two-way binding passes the text on only when the box loses focus: Enter would search for the old text.
		ViewModel.Query = args.QueryText;
		ViewModel.SearchCommand.Execute(null);
	}

	// Double-clicking a chapter reads it, as picking it and Read would.
	private void OnChapterDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e) => ViewModel?.ReadCommand.Execute(null);
}
