
namespace Totoro.Core.ViewModels.Discover;

public class MyAnimeListDiscoverViewModel : NavigatableViewModel
{
    private readonly IMyAnimeListService _myAnimeListService;

    public MyAnimeListDiscoverViewModel(IMyAnimeListService myAnimeListService)
    {
        _myAnimeListService = myAnimeListService;
    }

    [Reactive] public bool IsLoading { get; set; }
    public ObservableCollection<NamedAnimeList> Lists { get; } = [];


    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        IsLoading = true;

        Lists.Add(new("Top Airing", await _myAnimeListService.GetAiringAnime().ToListAsync()));
        Lists.Add(new("Upcomming", await _myAnimeListService.GetUpcomingAnime().ToListAsync()));
        Lists.Add(new("Popular", await _myAnimeListService.GetPopularAnime().ToListAsync()));
        Lists.Add(new("Recommended", await _myAnimeListService.GetRecommendedAnime().ToListAsync()));

        IsLoading = false;
    }
}

public class NamedAnimeList(string name, IEnumerable<AnimeModel> list)
{
    public string Name { get; } = name;
    public List<AnimeModel> List { get; } = list.ToList();
}