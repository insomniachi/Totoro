using System.Reactive.Concurrency;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel
{
    private readonly INavigationService _navigationService;
    private readonly IConnectivityService _connectivityService;
    private readonly SourceCache<IAiredAnimeEpisode, string> _episodesCache = new(x => (x.Url + x.EpisodeString));
    private readonly SourceCache<ICatalogItem, string> _animeSearchResultCache = new(x => x.Url);
    private readonly ReadOnlyObservableCollection<IAiredAnimeEpisode> _episodes;
    private readonly ReadOnlyObservableCollection<ICatalogItem> _animeSearchResults;
    private readonly AnimeProvider _provider;

    public DiscoverViewModel(IPluginFactory<AnimeProvider> providerFacotory,
                             ISettings settings,
                             INavigationService navigationService,
                             IConnectivityService connectivityService)
    {
        _provider = providerFacotory.CreatePlugin(settings.DefaultProviderType);
        _navigationService = navigationService;
        _connectivityService = connectivityService;
        _episodesCache
            .Connect()
            .RefCount()
            .Filter(this.WhenAnyValue(x => x.FilterText).Select(FilterByTitle))
            .Sort(SortExpressionComparer<IAiredAnimeEpisode>.Ascending(x => _episodesCache.Items.IndexOf(x)))
            .Bind(out _episodes)
            .DisposeMany()
            .Subscribe()
            .DisposeWith(Garbage);

        _animeSearchResultCache
            .Connect()
            .RefCount()
            .Sort(SortExpressionComparer<ICatalogItem>.Ascending(x => x.Title))
            .Bind(out _animeSearchResults)
            .DisposeMany()
            .Subscribe()
            .DisposeWith(Garbage);

        CardWidth = settings.DefaultProviderType is "anime-pahe" ? 480 : 190; // animepahe image is thumbnail
        DontUseImageEx = settings.DefaultProviderType is "yugen-anime"; // using imagex for yugen is crashing

        SelectEpisode = ReactiveCommand.CreateFromTask<IAiredAnimeEpisode>(OnEpisodeSelected);
        SelectSearchResult = ReactiveCommand.CreateFromTask<ICatalogItem>(OnSearchResultSelected);
        LoadMore = ReactiveCommand.Create(LoadMoreEpisodes, this.WhenAnyValue(x => x.IsLoading).Select(x => !x));
        SearchProvider = ReactiveCommand.Create<string>(query =>
        {
            Observable
            .Start(() => Search(query))
            .Do(_ => SetLoading(true))
            .SelectMany(x => x)
            .Finally(() => SetLoading(false))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list => _animeSearchResultCache.EditDiff(list, (item1, item2) => item1.Url == item2.Url), RxApp.DefaultExceptionHandler.OnError);
        });

        this.WhenAnyValue(x => x.SearchText)
            .Where(x => x is { Length: >= 2 })
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(Search)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list => _animeSearchResultCache.EditDiff(list, (item1, item2) => item1.Url == item2.Url), RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.SearchText)
            .Where(x => x is { Length: < 2 } && AnimeSearchResults.Count > 0)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => _animeSearchResultCache.Clear());

        connectivityService
            .Connected
            .Where(_ => !_episodesCache.Items.Any())
            .SelectMany(_ => LoadPage(1))
            .Subscribe();
    }

    [Reactive] public int SelectedIndex { get; set; }
    [Reactive] public bool ShowOnlyWatchingAnime { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string FilterText { get; set; }
    [Reactive] public bool DontUseImageEx { get; private set; }
    [Reactive] public string SearchText { get; set; }
    [Reactive] public int TotalPages { get; set; } = 1;

    public bool IsAuthenticated { get; }
    public double CardWidth { get; }
    public ReadOnlyObservableCollection<IAiredAnimeEpisode> Episodes => _episodes;
    public ReadOnlyObservableCollection<ICatalogItem> AnimeSearchResults => _animeSearchResults;

    public ICommand SelectEpisode { get; }
    public ICommand SelectFeaturedAnime { get; }
    public ICommand LoadMore { get; }
    public ICommand SelectSearchResult { get; }
    public ICommand SearchProvider { get; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if(!_connectivityService.IsConnected)
        {
            return Task.CompletedTask;
        }

        LoadPage(1).Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);

        return Task.CompletedTask;
    }

    private void LoadMoreEpisodes() =>
        LoadPage(TotalPages + 1)
        .Finally(() => TotalPages++)
        .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);

    private Task OnEpisodeSelected(IAiredAnimeEpisode episode)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["EpisodeInfo"] = episode
        };

        _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);

        return Task.CompletedTask;
    }

    private Task OnSearchResultSelected(ICatalogItem searchResult)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["SearchResult"] = searchResult
        };

        _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);

        return Task.CompletedTask;
    }


    private IObservable<IAiredAnimeEpisode> LoadPage(int page)
    {
        if (_provider?.AiredAnimeEpisodeProvider is null)
        {
            return Observable.Empty<IAiredAnimeEpisode>();
        }

        IsLoading = true;

        return _provider?
             .AiredAnimeEpisodeProvider
             .GetRecentlyAiredEpisodes(page)
             .ToObservable()
             .ObserveOn(RxApp.MainThreadScheduler)
             .Do(eps =>
             {
                 _episodesCache.AddOrUpdate(eps);
                 IsLoading = false;
             });
    }

    private void SetLoading(bool isLoading)
    {
        RxApp.MainThreadScheduler.Schedule(() => IsLoading = isLoading);
    }

    private Task<List<ICatalogItem>> Search(string term)
    {
        if (_provider is null)
        {
            return Task.FromResult(new List<ICatalogItem>());
        }

        return _provider.Catalog.Search(term).ToListAsync().AsTask();
    }

    private static Func<IAiredAnimeEpisode, bool> FilterByTitle(string title) => (IAiredAnimeEpisode ae) => string.IsNullOrEmpty(title) || ae.Title.ToLower().Contains(title.ToLower());
}