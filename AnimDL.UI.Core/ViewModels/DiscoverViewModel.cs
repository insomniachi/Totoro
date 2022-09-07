namespace AnimDL.UI.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel, IHaveState
{
    private readonly IRecentEpisodesProvider _recentEpisodesProvider;
    private readonly IFeaturedAnimeProvider _featuredAnimeProvider;
    private readonly INavigationService _navigationService;
    private readonly SourceCache<AiredEpisode, string> _episodesCache = new(x => x.Anime);
    private readonly ReadOnlyObservableCollection<AiredEpisode> _episodes;

    public DiscoverViewModel(IRecentEpisodesProvider recentEpisodesProvider,
                             IFeaturedAnimeProvider featuredAnimeProvider,
                             INavigationService navigationService)
    {
        _recentEpisodesProvider = recentEpisodesProvider;
        _featuredAnimeProvider = featuredAnimeProvider;
        _navigationService = navigationService;

        _episodesCache
            .Connect()
            .RefCount()
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
    public ReadOnlyObservableCollection<AiredEpisode> Episodes => _episodes;
    public ICommand SelectEpisode { get; }
    public ICommand SelectFeaturedAnime { get; }

    public void RestoreState(IState state)
    {
        Featured = state.GetValue<IList<FeaturedAnime>>(nameof(Featured));
        ShowOnlyWatchingAnime = state.GetValue<bool>(nameof(ShowOnlyWatchingAnime));
    }

    public Task SetInitialState()
    {
        _featuredAnimeProvider.GetFeaturedAnime()
                              .ObserveOn(RxApp.MainThreadScheduler)
                              .Subscribe(featured => Featured = featured.ToList());
        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(Featured);
        state.AddOrUpdate(ShowOnlyWatchingAnime);
    }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {

        _recentEpisodesProvider.GetRecentlyAiredEpisodes()
                               .ObserveOn(RxApp.MainThreadScheduler)
                               .Subscribe(eps => _episodesCache.AddOrUpdate(eps));

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

    private static Func<AiredEpisode, bool> Filter(bool value) => x => !value || x.Model is { Tracking.Status: AnimeStatus.Watching };
}