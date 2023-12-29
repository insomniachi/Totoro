using System.Reactive.Concurrency;
using Totoro.Plugins;
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

    public DiscoverViewModel(IPluginFactory<AnimeProvider> providerFacotory,
                             ISettings settings,
                             INavigationService navigationService,
                             IConnectivityService connectivityService)
    {
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

        Plugins = providerFacotory.Plugins.ToList();
        SelectedProvider = providerFacotory.Plugins.FirstOrDefault(x => x.Name == settings.DefaultProviderType);

        SelectEpisode = ReactiveCommand.CreateFromTask<IAiredAnimeEpisode>(OnEpisodeSelected);
        SelectSearchResult = ReactiveCommand.CreateFromTask<ICatalogItem>(OnSearchResultSelected);
        LoadMore = ReactiveCommand.Create(LoadMoreEpisodes, this.WhenAnyValue(x => x.IsEpisodesLoading).Select(x => !x));
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

        this.WhenAnyValue(x => x.SelectedProvider)
            .WhereNotNull()
            .Do(provider =>
            {
                CardWidth = provider.Name is "anime-pahe" or "marin" ? 480 : 190; // animepahe image is thumbnail
                DontUseImageEx = settings.DefaultProviderType is "yugen-anime"; // using imagex for yugen is crashing
            })
            .Select(providerInfo => providerFacotory.CreatePlugin(providerInfo.Name))
            .ToPropertyEx(this, x => x.Provider);

        this.WhenAnyValue(x => x.Provider)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ =>
            {
                _episodesCache.Clear();
                _animeSearchResultCache.Clear();
            })
            .SelectMany(_ => LoadPage(1))
            .Subscribe();

        connectivityService
            .Connected
            .Where(_ => !_episodesCache.Items.Any())
            .SelectMany(_ => LoadPage(1))
            .Subscribe();
    }

    [Reactive] public int SelectedIndex { get; set; }
    [Reactive] public bool ShowOnlyWatchingAnime { get; set; }
    [Reactive] public bool IsEpisodesLoading { get; set; }
    [Reactive] public bool IsSearchResultsLoading { get; set; }
    [Reactive] public string FilterText { get; set; }
    [Reactive] public bool DontUseImageEx { get; private set; }
    [Reactive] public string SearchText { get; set; }
    [Reactive] public int TotalPages { get; set; } = 1;
    [Reactive] public PluginInfo SelectedProvider { get; set; }
    [Reactive] public double CardWidth { get; set; }
    [ObservableAsProperty] public AnimeProvider Provider { get; }
    
    public List<PluginInfo> Plugins { get; }
    public bool IsAuthenticated { get; }
    public ReadOnlyObservableCollection<IAiredAnimeEpisode> Episodes => _episodes;
    public ReadOnlyObservableCollection<ICatalogItem> AnimeSearchResults => _animeSearchResults;

    public ICommand SelectEpisode { get; }
    public ICommand SelectFeaturedAnime { get; }
    public ICommand LoadMore { get; }
    public ICommand SelectSearchResult { get; }
    public ICommand SearchProvider { get; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (!_connectivityService.IsConnected)
        {
            return Task.CompletedTask;
        }

        //LoadPage(1).Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);

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
        if (Provider?.AiredAnimeEpisodeProvider is null)
        {
            return Observable.Empty<IAiredAnimeEpisode>();
        }

        IsEpisodesLoading = true;

        return Provider?
             .AiredAnimeEpisodeProvider
             .GetRecentlyAiredEpisodes(page)
             .ToObservable()
             .ObserveOn(RxApp.MainThreadScheduler)
             .Do(eps =>
             {
                 _episodesCache.AddOrUpdate(eps);
                 IsEpisodesLoading = false;
             });
    }

    private void SetLoading(bool isLoading)
    {
        RxApp.MainThreadScheduler.Schedule(() => IsSearchResultsLoading = isLoading);
    }

    private Task<List<ICatalogItem>> Search(string term)
    {
        if (Provider is null)
        {
            return Task.FromResult(new List<ICatalogItem>());
        }

        return Provider.Catalog.Search(term).ToListAsync().AsTask();
    }

    private static Func<IAiredAnimeEpisode, bool> FilterByTitle(string title) => (IAiredAnimeEpisode ae) => string.IsNullOrEmpty(title) || ae.Title.Contains(title, StringComparison.CurrentCultureIgnoreCase);
}