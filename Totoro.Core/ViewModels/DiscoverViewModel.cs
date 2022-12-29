namespace Totoro.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel, IHaveState
{
    private readonly INavigationService _navigationService;
    private readonly ITrackingService _trackingService;
    private readonly ISchedulerProvider _schedulerProvider;
    private readonly SourceCache<AiredEpisode, string> _episodesCache = new(x => x.Url);
    private readonly ReadOnlyObservableCollection<AiredEpisode> _episodes;
    private readonly IProvider _provider;
    private List<AnimeModel> _userAnime = new();

    public DiscoverViewModel(IProviderFactory providerFacotory,
                             ISettings settings,
                             INavigationService navigationService,
                             ITrackingService trackingService,
                             ISchedulerProvider schedulerProvider)
    {
        _provider = providerFacotory.GetProvider(settings.DefaultProviderType);
        _navigationService = navigationService;
        _trackingService = trackingService;
        _schedulerProvider = schedulerProvider;

        _episodesCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.FilterText).Select(FilterByTitle))
            .Sort(SortExpressionComparer<AiredEpisode>.Ascending(x => _episodesCache.Items.IndexOf(x)))
            .Page(this.WhenAnyValue(x => x.PagerViewModel).WhereNotNull().SelectMany(x => x.AsPager()))
            .Do(changes => PagerViewModel?.Update(changes.Response))
            .Bind(out _episodes)
            .DisposeMany()
            .Subscribe()
            .DisposeWith(Garbage);

        CardWidth = settings.DefaultProviderType == ProviderType.AnimePahe ? 480 : 190; // animepahe image is thumbnail
        ShowOnlyWatchingAnime = IsAuthenticated = trackingService.IsAuthenticated;
        DontUseImageEx = settings.DefaultProviderType == ProviderType.Yugen; // using imagex for yugen is crashing

        SelectEpisode = ReactiveCommand.CreateFromTask<AiredEpisode>(OnEpisodeSelected);
        SelectFeaturedAnime = ReactiveCommand.Create<FeaturedAnime>(OnFeaturedAnimeSelected);
        LoadMore = ReactiveCommand.Create(LoadMoreEpisodes, this.WhenAnyValue(x => x.IsLoading).Select(x => !x));
    }

    [Reactive] public int SelectedIndex { get; set; }
    [Reactive] public bool ShowOnlyWatchingAnime { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public PagerViewModel PagerViewModel { get; set; }
    [Reactive] public string FilterText { get; set; }
    [Reactive] public bool DontUseImageEx { get; private set; }
    
    public bool IsAuthenticated { get; }
    public double CardWidth { get; }
    public ReadOnlyObservableCollection<AiredEpisode> Episodes => _episodes;
    
    public ICommand SelectEpisode { get; }
    public ICommand SelectFeaturedAnime { get; }
    public ICommand LoadMore { get; }

    public void RestoreState(IState state)
    {
        ShowOnlyWatchingAnime = state.GetValue<bool>(nameof(ShowOnlyWatchingAnime));
        _userAnime = state.GetValue<List<AnimeModel>>("UserAnime");
    }

    public Task SetInitialState()
    {
        _trackingService
            .GetAnime()
            .Do(_userAnime.AddRange)
            .ObserveOn(_schedulerProvider.MainThreadScheduler)
            .Do(_ => _episodesCache.Refresh())
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError)
            .DisposeWith(Garbage);

        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(ShowOnlyWatchingAnime);
        state.AddOrUpdate(_userAnime, "UserAnime");
    }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        LoadPage(1)
            .Finally(() => PagerViewModel = new(0, _episodesCache.Items.Count()))
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);

        return Task.CompletedTask;
    }

    private void LoadMoreEpisodes() => 
        LoadPage((PagerViewModel?.PageCount ?? 1) + 1)
        .Finally(() =>
        {
            if(PagerViewModel.CurrentPage == PagerViewModel.PageCount - 2)
            {
                PagerViewModel.CurrentPage++;
            }
        })
        .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);

    private Task OnEpisodeSelected(AiredEpisode episode)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["EpisodeInfo"] = episode
        };

        _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);

        return Task.CompletedTask;
    }

    private void OnFeaturedAnimeSelected(FeaturedAnime anime)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["Id"] = long.Parse(anime.Id)
        };

        _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);
    }

    private IObservable<IEnumerable<AiredEpisode>> LoadPage(int page)
    {
        if (_provider.AiredEpisodesProvider is null)
        {
            return Observable.Empty<IEnumerable<AiredEpisode>>();
        }

        IsLoading = true;

        return _provider
             .AiredEpisodesProvider
             .GetRecentlyAiredEpisodes(page)
             .ToObservable()
             .ObserveOn(RxApp.MainThreadScheduler)
             .Do(eps =>
             {
                 _episodesCache.AddOrUpdate(eps);
                 IsLoading = false;
             });
    }

    private static Func<AiredEpisode, bool> FilterByTitle(string title) => (AiredEpisode ae) => string.IsNullOrEmpty(title) || ae.Title.ToLower().Contains(title.ToLower());
}