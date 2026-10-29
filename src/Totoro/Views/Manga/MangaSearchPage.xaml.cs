using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Manga;

namespace Totoro.Views.Manga;

public sealed partial class MangaSearchPage : Page
{
	public MangaSearchPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MangaSearchViewModel? ViewModel { get; set; }
}
