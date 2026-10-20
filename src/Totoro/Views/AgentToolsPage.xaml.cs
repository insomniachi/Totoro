using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class AgentToolsPage : Page
{
	public AgentToolsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AgentToolsViewModel? ViewModel { get; set; }
}
