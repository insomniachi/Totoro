using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Overview tab: facts, genres, the user's list entry, synopsis and studios.</summary>
public sealed partial class OverviewTabView : UserControl
{
	public OverviewTabView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AnimeDetailsViewModel? ViewModel { get; set; }
}