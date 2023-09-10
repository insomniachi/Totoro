using System.Reactive.Concurrency;
using Totoro.Plugins;
using Totoro.Plugins.Manga;
using Totoro.Plugins.Manga.Contracts;

namespace Totoro.Core.ViewModels;

public class DiscoverMangaViewModel : NavigatableViewModel
{
    private readonly SourceCache<ICatalogItem, string> _mangaSearchResultCache = new(x => x.Url);
    private readonly ReadOnlyObservableCollection<ICatalogItem> _mangaSearchResults;
    private readonly INavigationService _navigationService;
    private readonly MangaProvider _provider;
    public DiscoverMangaViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        _mangaSearchResultCache
           .Connect()
           .RefCount()
           .Sort(SortExpressionComparer<ICatalogItem>.Ascending(x => x.Title))
           .Bind(out _mangaSearchResults)
           .DisposeMany()
           .Subscribe()
           .DisposeWith(Garbage);

        _provider = PluginFactory<MangaProvider>.Instance.CreatePlugin("manga-dex");

        this.WhenAnyValue(x => x.SearchText)
            .Where(x => x is { Length: >= 2 })
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async query => await Search(query), RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.SearchText)
            .Where(x => x is { Length: < 2 } && MangaSearchResults.Count > 0)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => _mangaSearchResultCache.Clear());

        SelectSearchResult = ReactiveCommand.CreateFromTask<ICatalogItem>(OnSearchResultSelected);
        SearchProvider = ReactiveCommand.CreateFromTask<string>(Search);
    }

    [Reactive] public string SearchText { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    public ReadOnlyObservableCollection<ICatalogItem> MangaSearchResults => _mangaSearchResults;
    public ICommand SearchProvider { get; }
    public ICommand SelectSearchResult { get; set; }

    private void SetLoading(bool isLoading)
    {
        RxApp.MainThreadScheduler.Schedule(() => IsLoading = isLoading);
    }

    private Task OnSearchResultSelected(ICatalogItem searchResult)
    {
        var navigationParameters = new Dictionary<string, object>
        {
            ["SearchResult"] = searchResult
        };

        //_navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);

        return Task.CompletedTask;
    }

    private async Task Search(string query)
    {
        if (_provider is null)
        {
            return;
        }

        await foreach(var item in _provider.Catalog.Search(query))
        {
            _mangaSearchResultCache.AddOrUpdate(item);
        }
    }

}
