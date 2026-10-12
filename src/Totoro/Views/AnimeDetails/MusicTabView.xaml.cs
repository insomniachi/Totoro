using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Music tab. Leaving it (another tab, or closing the details) stops the song playing.</summary>
public sealed partial class MusicTabView : UserControl
{
	public MusicTabView()
	{
		InitializeComponent();
		Unloaded += (_, _) => ViewModel?.StopAudio();
	}

	[GeneratedDependencyProperty]
	public partial MusicTab? ViewModel { get; set; }
}
