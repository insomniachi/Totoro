namespace Totoro.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel
{
    private readonly INavigationService _navigationService;
    private readonly SourceCache<AiredEpisode, string> _episodesCache = new(x => x.Url);
    private readonly SourceCache<SearchResult, string> _animeSearchResultCache = new(x => x.Url);
    private readonly ReadOnlyObservableCollection<AiredEpisode> _episodes;
    private readonly ReadOnlyObservableCollection<SearchResult> _animeSearchResults;
    private readonly IProvider _provider;

    public DiscoverViewModel(IProviderFactory providerFacotory,
                             ISettings settings,
                             INavigationService navigationService)
    {
        _provider = providerFacotory.GetProvider(settings.DefaultProviderType);
        _navigationService = navigationService;

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

        _animeSearchResultCache
            .Connect()
            .RefCount()
            .Bind(out _animeSearchResults)
            .DisposeMany()
            .Subscribe()
            .DisposeWith(Garbage);

        CardWidth = settings.DefaultProviderType is ProviderType.AnimePahe or ProviderType.Marin ? 480 : 190; // animepahe image is thumbnail
        DontUseImageEx = settings.DefaultProviderType == ProviderType.Yugen; // using imagex for yugen is crashing

        SelectEpisode = ReactiveCommand.CreateFromTask<AiredEpisode>(OnEpisodeSelected);
        SelectSearchResult = ReactiveCommand.CreateFromTask<SearchResult>(OnSearchResultSelected);
        LoadMore = ReactiveCommand.Create(LoadMoreEpisodes, this.WhenAnyValue(x => x.IsLoading).Select(x => !x));

        this.WhenAnyValue(x => x.SearchText)
            .Where(x => x is { Length: >= 2 })
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(term => _provider.Catalog.Search(term).ToListAsync().AsTask())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list => _animeSearchResultCache.EditDiff(list, (item1, item2) => item1.Url == item2.Url), RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.SearchText)
            .Where(x => x is { Length: < 2 } && AnimeSearchResults.Count > 0)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => _animeSearchResultCache.Clear());
    }

    [Reactive] public int SelectedIndex { get; set; }
    [Reactive] public bool ShowOnlyWatchingAnime { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public PagerViewModel PagerViewModel { get; set; }
    [Reactive] public string FilterText { get; set; }
    [Reactive] public bool DontUseImageEx { get; private set; }
    [Reactive] public string SearchText { get; set; }

    public bool IsAuthenticated { get; }
    public double CardWidth { get; }
    public ReadOnlyObservableCollection<AiredEpisode> Episodes => _episodes;
    public ReadOnlyObservableCollection<SearchResult> AnimeSearchResults => _animeSearchResults;

    public ICommand SelectEpisode { get; }
    public ICommand SelectFeaturedAnime { get; }
    public ICommand LoadMore { get; }
    public ICommand SelectSearchResult { get; }

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
            if (PagerViewModel.CurrentPage == PagerViewModel.PageCount - 2)
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

    private Task OnSearchResultSelected(SearchResult searchResult)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["SearchResult"] = searchResult
        };

        _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);

        return Task.CompletedTask;
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