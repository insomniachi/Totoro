using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Test;

namespace Totoro.Views.Test;

public sealed partial class HtmlRendererPage : Page
{
	public HtmlRendererPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial HtmlRendererViewModel? ViewModel { get; set; }
}
