using Totoro.Core.ViewModels.About;

namespace Totoro.WinUI.Views.AboutSections;

public class AnimeCardListSectionBase : ReactivePage<AnimeCardListViewModel> { }

public sealed partial class AnimeCardListSection : AnimeCardListSectionBase
{
    public AnimeCardListSection()
    {
        InitializeComponent();
    }
}
