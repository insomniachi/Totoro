using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class AnimeSettingsDialog : UserControl
{
	public AnimeSettingsDialog()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AnimeSettingsDialogViewModel? ViewModel { get; set; }
}
