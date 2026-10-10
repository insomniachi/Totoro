using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Episodes tab.</summary>
public sealed partial class EpisodesTabView : UserControl
{
	public EpisodesTabView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial EpisodesTab? ViewModel { get; set; }
}