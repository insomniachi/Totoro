using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views.Settings;

/// <summary>The settings every player shares: shortcuts, languages, volume... (<see cref="UI.Player.PlayerSettings"/>).</summary>
public sealed partial class PlayerSettingsPage : Page
{
	public PlayerSettingsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SettingsViewModel? ViewModel { get; set; }
}
