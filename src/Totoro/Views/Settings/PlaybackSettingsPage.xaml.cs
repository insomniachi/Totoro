using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views.Settings;

public sealed partial class PlaybackSettingsPage : Page
{
	public PlaybackSettingsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SettingsViewModel? ViewModel { get; set; }
}
