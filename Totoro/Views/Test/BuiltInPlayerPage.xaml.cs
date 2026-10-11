using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Test;

namespace Totoro.Views.Test;

public sealed partial class BuiltInPlayerPage : Page
{
	public BuiltInPlayerPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial BuiltInPlayerViewModel? ViewModel { get; set; }
}
