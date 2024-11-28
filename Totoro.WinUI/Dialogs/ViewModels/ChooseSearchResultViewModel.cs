using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.WinUI.Dialogs.ViewModels;

public sealed class ChooseSearchResultViewModel : ReactiveObject
{
    public readonly CompositeDisposable Garbage = [];
    private readonly SourceCache<ICatalogItem, string> _searchResultCache = new(x => x.Title);
    private readonly ReadOnlyObservableCollection<ICatalogItem> _searchResults;
    private readonly ObservableAsPropertyHelper<AnimeProvider> _provider;
    public ChooseSearchResultViewModel(IPluginFactory<AnimeProvider> providerFactory)
    {

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Select(providerFactory.CreatePlugin)
            .ToProperty(this, x => x.Provider, out _provider);

        this.ObservableForProperty(x => x.Title, x => x)
            .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler)
            .SelectMany(x => Provider.Catalog.Search(x).ToListAsync().AsTask())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => _searchResultCache.EditDiff(x, (first, second) => first.Title == second.Title));

        _searchResultCache
            .Connect()
            .RefCount()
            .ObserveOn(RxApp.MainThreadScheduler)
            .SortAndBind(out _searchResults, SortExpressionComparer<ICatalogItem>.Ascending(x => x.Title))
            .Bind(out _searchResults)
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);
    }

    public void Dispose() => Garbage.Dispose();

    [Reactive] public ICatalogItem SelectedSearchResult { get; set; }
    [Reactive] public string Title { get; set; }
    [Reactive] public string SelectedProviderType { get; set; } = "allanime";
    public IEnumerable<ICatalogItem> SearchResults => _searchResults;
    public List<string> Providers { get; set; } = [];
    public AnimeProvider Provider => _provider.Value;
    public void SetValues(IEnumerable<ICatalogItem> values)
    {
        _searchResultCache.Clear();
        _searchResultCache.Edit(x => x.AddOrUpdate(values));
    }
}
