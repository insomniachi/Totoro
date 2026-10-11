using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Test;

namespace Totoro.Views.Test;

public sealed partial class AgentToolsPage : Page
{
	public AgentToolsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AgentToolsViewModel? ViewModel { get; set; }
}
