using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Music tab. A song played in it plays on after it's left, with the main window's now-playing bar to control it.</summary>
public sealed partial class MusicTabView : UserControl
{
	public MusicTabView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MusicTab? ViewModel { get; set; }
}
