using System.Reactive.Concurrency;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Core.ViewModels.Discover;

public class SearchProviderViewModel : NavigatableViewModel
{
    private readonly INavigationService _navigationService;
    private readonly SourceCache<ICatalogItem, string> _animeSearchResultCache = new(x => x.Url);
    private readonly ReadOnlyObservableCollection<ICatalogItem> _animeSearchResults;

    public SearchProviderViewModel(IPluginFactory<AnimeProvider> providerFacotory,
                                   ISettings settings,
                                   INavigationService navigationService)
    {
        _navigationService = navigationService;

        Plugins = providerFacotory.Plugins.ToList();
        SelectedProvider = providerFacotory.Plugins.FirstOrDefault(x => x.Name == settings.DefaultProviderType);

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
        SelectSearchResult = ReactiveCommand.CreateFromTask<ICatalogItem>(OnSearchResultSelected);

        _animeSearchResultCache
            .Connect()
            .RefCount()
            .SortAndBind(out _animeSearchResults, SortExpressionComparer<ICatalogItem>.Ascending(x => x.Title))
            .DisposeMany()
            .Subscribe()
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.SelectedProvider)
            .WhereNotNull()
            .Select(providerInfo => providerFacotory.CreatePlugin(providerInfo.Name))
            .ToPropertyEx(this, x => x.Provider);

        this.WhenAnyValue(x => x.Provider)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ => _animeSearchResultCache.Clear())
            .Subscribe();

        this.WhenAnyValue(x => x.SearchText)
            .Where(x => x is { Length: >= 2 })
            .Throttle(TimeSpan.FromSeconds(1))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(Search)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list => _animeSearchResultCache.EditDiff(list, (item1, item2) => item1.Url == item2.Url), RxApp.DefaultExceptionHandler.OnError);
    }

    [Reactive] public string SearchText { get; set; }
    [Reactive] public PluginInfo SelectedProvider { get; set; }
    [Reactive] public bool IsSearchResultsLoading { get; set; }

    [ObservableAsProperty] public AnimeProvider Provider { get; }

    public List<PluginInfo> Plugins { get; }
    public ReadOnlyObservableCollection<ICatalogItem> AnimeSearchResults => _animeSearchResults;

    public ICommand SelectSearchResult { get; }
    public ICommand SearchProvider { get; }

    private void SetLoading(bool isLoading)
    {
        RxApp.MainThreadScheduler.Schedule(() => IsSearchResultsLoading = isLoading);
    }

    private Task OnSearchResultSelected(ICatalogItem searchResult)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            [WatchViewModelParamters.SearchResult] = searchResult,
            [WatchViewModelParamters.Provider] = SelectedProvider.Name
        };

        _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);

        return Task.CompletedTask;
    }

    private Task<List<ICatalogItem>> Search(string term)
    {
        if (Provider is null)
        {
            return Task.FromResult(new List<ICatalogItem>());
        }

        return Provider.Catalog.Search(term).ToListAsync().AsTask();
    }
}
