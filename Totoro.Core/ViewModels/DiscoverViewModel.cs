using Totoro.Core.ViewModels.Discover;

namespace Totoro.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel
{
    public DiscoverViewModel()
    {

    }

    [Reactive] public PivotItemModel SelectedSection { get; set; }

    public ObservableCollection<PivotItemModel> Sections { get; } =
    [
        new PivotItemModel { Header = "Recently Aired", ViewModel = typeof(RecentEpisodesViewModel) },
        new PivotItemModel { Header = "Search" , ViewModel = typeof(SearchProviderViewModel) }
    ];
}