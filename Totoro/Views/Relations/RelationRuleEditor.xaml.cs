using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.ViewModels.Relations;

namespace Totoro.Views.Relations;

/// <summary>Edits an anime-relations rule (see <see cref="RelationRuleEditorViewModel"/>).</summary>
public sealed partial class RelationRuleEditor : UserControl
{
	public RelationRuleEditor()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial RelationRuleEditorViewModel? ViewModel { get; set; }

	partial void OnViewModelChanged(RelationRuleEditorViewModel? newValue)
	{
		// The boxes show the anime chosen; for a rule being edited, the ones it has.
		SeasonBox.Text = newValue?.Season?.Title ?? "";
		EarlierBox.Text = newValue?.Earlier?.Title ?? "";
	}

	/// <summary>The chosen season stands out in the list of the series' seasons.</summary>
	public static Brush SeasonForeground(bool isChosen) =>
		(Brush)Application.Current.Resources[isChosen ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"];

	private void OnSeasonTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		if (ViewModel is { } viewModel && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
		{
			// Typing replaces the choice; it's made again from the suggestions.
			viewModel.Season = null;
			_ = viewModel.SearchSeasonAsync(sender.Text);
		}
	}

	private void OnSeasonChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
	{
		ViewModel?.Season = args.SelectedItem as RelationAnime;
	}

	private void OnEarlierTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		if (ViewModel is { } viewModel && args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
		{
			viewModel.Earlier = null;
			_ = viewModel.SearchEarlierAsync(sender.Text);
		}
	}

	private void OnEarlierChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
	{
		ViewModel?.Earlier = args.SelectedItem as RelationAnime;
	}
}
