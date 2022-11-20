using DynamicData;
using HtmlAgilityPack;

namespace Totoro.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel, IHaveState
{
    private readonly IRecentEpisodesProvider _recentEpisodesProvider;
    private readonly IFeaturedAnimeProvider _featuredAnimeProvider;
    private readonly INavigationService _navigationService;
    private readonly ITrackingService _trackingService;
    private readonly SourceCache<AiredEpisode, string> _episodesCache = new(x => x.Anime);
    private readonly ReadOnlyObservableCollection<AiredEpisode> _episodes;
    private List<AnimeModel> _userAnime = new();

    public DiscoverViewModel(IRecentEpisodesProvider recentEpisodesProvider,
                             IFeaturedAnimeProvider featuredAnimeProvider,
                             INavigationService navigationService,
                             ITrackingService trackingService)
    {
        _recentEpisodesProvider = recentEpisodesProvider;
        _featuredAnimeProvider = featuredAnimeProvider;
        _navigationService = navigationService;
        _trackingService = trackingService;
        _episodesCache
            .Connect()
            .RefCount()
            .Sort(SortExpressionComparer<AiredEpisode>.Descending(x => x.TimeOfAiring))
            .Filter(this.WhenAnyValue(x => x.ShowOnlyWatchingAnime).Select(Filter))
            .Bind(out _episodes)
            .DisposeMany()
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        SelectEpisode = ReactiveCommand.Create<AiredEpisode>(OnEpisodeSelected);
        SelectFeaturedAnime = ReactiveCommand.Create<FeaturedAnime>(OnFeaturedAnimeSelected);

        Observable
            .Timer(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(_ => Featured is not null)
            .Subscribe(_ =>
            {
                if (Featured.Count == 0)
                {
                    SelectedIndex = 0;
                    return;
                }

                if (SelectedIndex == Featured.Count - 1)
                {
                    SelectedIndex = 0;
                    return;
                }

                SelectedIndex++;
            });
    }


    [Reactive] public IList<FeaturedAnime> Featured { get; set; } = new List<FeaturedAnime>();
    [Reactive] public int SelectedIndex { get; set; }
    [Reactive] public bool ShowOnlyWatchingAnime { get; set; } = true;
    [Reactive] public bool IsLoading { get; set; }
    public ReadOnlyObservableCollection<AiredEpisode> Episodes => _episodes;
    public ICommand SelectEpisode { get; }
    public ICommand SelectFeaturedAnime { get; }

    public void RestoreState(IState state)
    {
        Featured = state.GetValue<IList<FeaturedAnime>>(nameof(Featured));
        ShowOnlyWatchingAnime = state.GetValue<bool>(nameof(ShowOnlyWatchingAnime));
        _userAnime = state.GetValue<List<AnimeModel>>("UserAnime");
    }

    public Task SetInitialState()
    {
        _featuredAnimeProvider
            .GetFeaturedAnime()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(featured => Featured = featured.ToList())
            .DisposeWith(Garbage);

        _trackingService
            .GetAnime()
            .Do(x => _userAnime.AddRange(x))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ => _episodesCache.Refresh())
            .Subscribe()
            .DisposeWith(Garbage);

        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(Featured);
        state.AddOrUpdate(ShowOnlyWatchingAnime);
        state.AddOrUpdate(_userAnime, "UserAnime");
    }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        IsLoading = true;

        _recentEpisodesProvider.GetRecentlyAiredEpisodes()
                               .ObserveOn(RxApp.MainThreadScheduler)
                               .Subscribe(eps =>
                               {
                                   _episodesCache.AddOrUpdate(eps);
                                   IsLoading = false;
                               })
                               .DisposeWith(Garbage);

        return Task.CompletedTask;
    }

    private void OnEpisodeSelected(AiredEpisode episode)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["EpisodeInfo"] = episode
        };

        _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);
    }

    private void OnFeaturedAnimeSelected(FeaturedAnime anime)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["Id"] = long.Parse(anime.Id)
        };

        _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);
    }

    private Func<AiredEpisode, bool> Filter(bool showOnlyUserAnime) => (ep) =>
    {
        if (!showOnlyUserAnime)
        {
            return true;
        }

        var model = _userAnime.FirstOrDefault(x => FuzzySharp.Fuzz.PartialRatio(ep.Anime, x.Title) > 80 || x.AlternativeTitles.Any(x => FuzzySharp.Fuzz.PartialRatio(ep.Anime, x) > 80));

        if (model is null)
        {
            return false;
        }
        else
        {
            return model.Tracking.UpdatedAt.HasValue && (DateTime.Today - model.Tracking.UpdatedAt.Value).TotalDays <= 7;
        }
    };
}