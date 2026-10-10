using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Related tab: related and recommended anime.</summary>
public sealed partial class RelatedTabView : UserControl
{
	public RelatedTabView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial RelatedTab? ViewModel { get; set; }
}