using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Manga;

namespace Totoro.Views.Manga;

public sealed partial class MangaTrackingDialog : UserControl
{
	public MangaTrackingDialog()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MangaTrackingDialogViewModel? ViewModel { get; set; }
}
