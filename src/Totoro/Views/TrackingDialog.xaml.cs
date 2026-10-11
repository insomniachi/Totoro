using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class TrackingDialog : UserControl
{
	public TrackingDialog()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial TrackingDialogViewModel? ViewModel { get; set; }
}
