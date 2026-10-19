using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class DownloadEpisodesDialog : UserControl
{
	public DownloadEpisodesDialog()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial DownloadEpisodesDialogViewModel? ViewModel { get; set; }
}
