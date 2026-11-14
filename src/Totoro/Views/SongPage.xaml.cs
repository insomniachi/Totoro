using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

/// <summary>A song's page, in the side pane.</summary>
public sealed partial class SongPage : Page
{
	public SongPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SongViewModel? ViewModel { get; set; }
}
