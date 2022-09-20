using AnimDL.Api;

namespace AnimDL.WinUI.Dialogs.ViewModels;

public sealed class ChooseSearchResultViewModel : ReactiveObject
{
    public readonly CompositeDisposable Garbage = new();
    private readonly SourceCache<SearchResult, string> _searchResultCache = new(x => x.Title);
    private readonly ReadOnlyObservableCollection<SearchResult> _searchResults;
    private readonly ObservableAsPropertyHelper<IProvider> _provider;
    public ChooseSearchResultViewModel(IProviderFactory providerFactory)
    {

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Select(x => providerFactory.GetProvider(x))
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
    [Reactive] public ProviderType SelectedProviderType { get; set; } = ProviderType.AnimixPlay;
    public IEnumerable<SearchResult> SearchResults => _searchResults;
    public List<ProviderType> Providers { get; set; } = Enum.GetValues<ProviderType>().Cast<ProviderType>().ToList();
    public IProvider Provider => _provider.Value;
    public void SetValues(IEnumerable<SearchResult> values)
    {
        _searchResultCache.Clear();
        _searchResultCache.Edit(x => x.AddOrUpdate(values));
    }
}
