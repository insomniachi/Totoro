using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Plugins;

namespace Totoro.Views.PluginTester;

/// <summary>A plugin test's status, or one of its stages', as an icon in the theme's status colors.</summary>
public sealed partial class TestStatusIcon : UserControl
{
	public TestStatusIcon()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial PluginTestStageStatus Status { get; set; }

	public Visibility VisibleIf(PluginTestStageStatus status, string name) =>
		status.ToString() == name ? Visibility.Visible : Visibility.Collapsed;
}
