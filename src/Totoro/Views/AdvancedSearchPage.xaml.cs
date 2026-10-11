using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
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

	/// <summary>Number of shimmer cards shown while searching; enough to fill a large window.</summary>
	public IReadOnlyList<int> PlaceholderCards { get; } = [.. Enumerable.Range(0, 12)];

	// For the sort order button's x:Bind function bindings (never given null: SortOrderText is always set).

	/// <summary>Up arrow for ascending, down for descending.</summary>
	public static string SortOrderGlyph(bool isAscending) => isAscending ? "" : "";

	/// <summary>What the button shows and does, e.g. "Highest first. Click to reverse."</summary>
	public static string SortOrderName(string order) => $"{order}. Click to reverse.";

	/// <summary>Suggests the database's genres matching what's typed, leaving out ones already in either box.</summary>
	private void OnGenreTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		// The sender is the box's inner text box.
		if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput || ViewModel is not { } viewModel ||
			sender.FindAscendant<TokenizingTextBox>() is not { } box)
		{
			return;
		}

		box.SuggestedItemsSource = viewModel.AllGenres
											.Where(x => x.Contains(sender.Text.Trim(), StringComparison.OrdinalIgnoreCase))
											.Except(viewModel.IncludedGenres)
											.Except(viewModel.ExcludedGenres)
											.ToList();
	}

	/// <summary>Only lets in genres the database knows (with its spelling), and each genre only once.</summary>
	private void OnGenreAdding(TokenizingTextBox sender, TokenItemAddingEventArgs args)
	{
		var genre = ViewModel?.AllGenres.FirstOrDefault(x => x.Equals(args.TokenText.Trim(), StringComparison.OrdinalIgnoreCase));
		if (genre is null || ViewModel!.IncludedGenres.Contains(genre) || ViewModel.ExcludedGenres.Contains(genre))
		{
			args.Cancel = true;
			return;
		}

		args.Item = genre;
	}
}
