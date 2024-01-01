using Totoro.Core.ViewModels.Discover;

namespace Totoro.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel
{
    public DiscoverViewModel(ISettings settings)
    {
        Sections.First(x => x.ViewModel == typeof(MyAnimeListDiscoverViewModel)).Visible = settings.DefaultListService == ListServiceType.MyAnimeList;
    }

    [Reactive] public PivotItemModel SelectedSection { get; set; }

    public ObservableCollection<PivotItemModel> Sections { get; } =
    [
        new () { Header = "Recently Aired", ViewModel = typeof(RecentEpisodesViewModel) },
        new () { Header = "Discover", ViewModel = typeof(MyAnimeListDiscoverViewModel), Visible = false },
        new () { Header = "Search" , ViewModel = typeof(SearchProviderViewModel) }
    ];
}