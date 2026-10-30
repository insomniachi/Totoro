using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Anime;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class MissedRelationsPage : Page
{
	public MissedRelationsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MissedRelationsViewModel? ViewModel { get; set; }

	private void OnFilterChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
	{
		if (ViewModel is not null)
		{
			ViewModel.Filter = sender.SelectedItem?.Tag is string tag ? [.. tag.Split(',').Select(Enum.Parse<AnimeRelationType>)] : null;
		}
	}
}
