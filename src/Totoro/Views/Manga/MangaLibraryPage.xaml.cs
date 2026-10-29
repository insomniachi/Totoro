using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels.Manga;

namespace Totoro.Views.Manga;

public sealed partial class MangaLibraryPage : Page
{
	public MangaLibraryPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial MangaLibraryViewModel? ViewModel { get; set; }
}
