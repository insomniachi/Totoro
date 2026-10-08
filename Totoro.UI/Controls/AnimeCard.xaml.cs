using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Module.Anime;

namespace Totoro.UI.Controls;

public sealed partial class AnimeCard : UserControl
{
	public AnimeCard()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial AnimeModel? Anime { get; set; }
}
