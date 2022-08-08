using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AnimDL.Api;
using AnimDL.Core.Models;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.Dialogs.ViewModels;

public sealed class ChooseSearchResultViewModel : ReactiveObject
{
    public readonly CompositeDisposable Garbage = new();
    private readonly SourceCache<SearchResult, string> _searchResultCache = new(x => x.Title);
    private readonly ReadOnlyObservableCollection<SearchResult> _searchResults;
    public ChooseSearchResultViewModel(IProviderFactory providerFactory)
    {

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Subscribe(x => Provider = providerFactory.GetProvider(x));

        this.ObservableForProperty(x => x.Title, x => x)
            .Throttle(TimeSpan.FromMilliseconds(500), RxApp.TaskpoolScheduler)
            .SelectMany(async x => await Provider.Catalog.Search(x).ToListAsync())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                _searchResultCache.EditDiff(x, (first, second) => first.Title == second.Title);
            });

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
    public IProvider Provider { get; set; }
    public void SetValues(IEnumerable<SearchResult> values)
    {
        _searchResultCache.Clear();
        _searchResultCache.Edit(x => x.AddOrUpdate(values));
    }
}
