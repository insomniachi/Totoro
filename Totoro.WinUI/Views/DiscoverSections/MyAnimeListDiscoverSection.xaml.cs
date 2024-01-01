using Microsoft.UI.Xaml.Controls;
using Totoro.Core.ViewModels.Discover;

namespace Totoro.WinUI.Views.DiscoverSections;

public class MyAnimeListDiscoverSectionBase : ReactivePage<MyAnimeListDiscoverViewModel> { }

public sealed partial class MyAnimeListDiscoverSection : MyAnimeListDiscoverSectionBase
{
    public MyAnimeListDiscoverSection()
    {
        InitializeComponent();
    }
}
