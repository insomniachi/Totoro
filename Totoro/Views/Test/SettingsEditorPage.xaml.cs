using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Test;

namespace Totoro.Views.Test;

public sealed partial class SettingsEditorPage : Page
{
	public SettingsEditorPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SettingsEditorViewModel? ViewModel { get; set; }
}
