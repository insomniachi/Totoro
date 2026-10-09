using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class SettingsPage : Page
{
	public SettingsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SettingsViewModel? ViewModel { get; set; }
}
