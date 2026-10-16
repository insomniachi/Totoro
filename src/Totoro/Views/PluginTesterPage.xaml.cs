using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class PluginTesterPage : Page
{
	public PluginTesterPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial PluginTesterViewModel? ViewModel { get; set; }
}
