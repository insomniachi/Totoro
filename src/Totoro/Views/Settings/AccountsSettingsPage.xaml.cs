using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views.Settings;

public sealed partial class AccountsSettingsPage : Page
{
	public AccountsSettingsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SettingsViewModel? ViewModel { get; set; }
}
