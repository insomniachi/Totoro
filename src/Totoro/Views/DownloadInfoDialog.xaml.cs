using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class DownloadInfoDialog : UserControl
{
	public DownloadInfoDialog()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial DownloadInfoDialogViewModel? ViewModel { get; set; }
}
