using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Anime;
using Totoro.UI.Navigation;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Characters tab.</summary>
public sealed partial class CharactersTabView : UserControl
{
	public CharactersTabView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial CharactersTab? ViewModel { get; set; }

	// The anime details page is in the side pane: the character's page replaces it there, and Back returns to it.
	private async void OnCharacterClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: CharacterModel character } && Navigators.TryGetNavigator("pane", out var pane))
		{
			await pane.NavigateToDataAsync(character);
		}
	}
}