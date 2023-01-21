using AnimDL.Core.Api;

namespace Totoro.WinUI.Dialogs.ViewModels;

public sealed class ChooseSearchResultViewModel : ReactiveObject
{
    public readonly CompositeDisposable Garbage = new();
    private readonly SourceCache<SearchResult, string> _searchResultCache = new(x => x.Title);
    private readonly ReadOnlyObservableCollection<SearchResult> _searchResults;
    private readonly ObservableAsPropertyHelper<IProvider> _provider;
    public ChooseSearchResultViewModel(IProviderFactory providerFactory)
    {

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Select(providerFactory.GetProvider)
            .ToProperty(this, x => x.Provider, out _provider);

        this.ObservableForProperty(x => x.Title, x => x)
            .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler)
            .SelectMany(x => Provider.Catalog.Search(x).ToListAsync().AsTask())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => _searchResultCache.EditDiff(x, (first, second) => first.Title == second.Title));

        _searchResultCache
            .Connect()
            .RefCount()
            .Sort(SortExpressionComparer<SearchResult>.Ascending(x => x.Title))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _searchResults)
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);
    }

    public void Dispose() => Garbage.Dispose();

    [Reactive] public SearchResult SelectedSearchResult { get; set; }
    [Reactive] public string Title { get; set; }
    [Reactive] public string SelectedProviderType { get; set; } = "allanime";
    public IEnumerable<SearchResult> SearchResults => _searchResults;
    public List<string> Providers { get; set; } = new List<string>();
    public IProvider Provider => _provider.Value;
    public void SetValues(IEnumerable<SearchResult> values)
    {
        _searchResultCache.Clear();
        _searchResultCache.Edit(x => x.AddOrUpdate(values));
    }
}
