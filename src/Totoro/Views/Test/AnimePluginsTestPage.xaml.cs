using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Test;

namespace Totoro.Views.Test;

public sealed partial class AnimePluginsTestPage : Page
{
	public AnimePluginsTestPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AnimePluginsTestViewModel? ViewModel { get; set; }
}
