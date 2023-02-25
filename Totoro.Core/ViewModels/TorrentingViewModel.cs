using System.Reactive.Concurrency;
using Totoro.Core.Torrents;
using TorrentModel = Totoro.Core.Torrents.TorrentModel;

namespace Totoro.Core.ViewModels;

public enum SortMode
{
    Date,
    Seeders
}

public class TorrentingViewModel : NavigatableViewModel
{
    private readonly IDebridServiceContext _debridServiceContext;
    private readonly ITorrentCatalog _catalog;
    private readonly SourceCache<TorrentModel, string> _torrentsCache = new(x => x.Link);
    private readonly ReadOnlyObservableCollection<TorrentModel> _torrents;

    public TorrentingViewModel(IDebridServiceContext debridServiceContext,
                               ITorrentCatalog catalog)
    {
        _debridServiceContext = debridServiceContext;
        _catalog = catalog;

        var sort = this.WhenAnyValue(x => x.SortMode)
            .Select(sort => sort switch
            {
                SortMode.Date => SortExpressionComparer<TorrentModel>.Descending(x => x.Date),
                SortMode.Seeders => SortExpressionComparer<TorrentModel>.Descending(x => x.Seeders),
                _ => throw new NotSupportedException(),
            });

        _torrentsCache
            .Connect()
            .RefCount()
            .Sort(sort)
            .Bind(out _torrents)
            .Subscribe()
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.PastedTorrent.MagnetLink)
            .Where(x => !string.IsNullOrEmpty(x))
            .Subscribe(_ => PastedTorrent.State = TorrentState.Unknown);

        Search = ReactiveCommand.Create(OnSearch);
    }

    [Reactive] public string Query { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public SortMode SortMode { get; set; } = SortMode.Date;

    public TorrentModel PastedTorrent { get; } = new();

    public bool IsAuthenticted => _debridServiceContext.IsAuthenticated;
    public ReadOnlyObservableCollection<TorrentModel> Torrents => _torrents;

    public ICommand Search { get; }
    
    private void OnSearch()
    {
        IsLoading = true;

        _catalog.Search(Query)
                .ToListAsync()
                .AsTask()
                .ToObservable()
                .Finally(() => RxApp.MainThreadScheduler.Schedule(() => IsLoading = false))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(list => _torrentsCache.EditDiff(list, (first, second) => first.Link == second.Link), RxApp.DefaultExceptionHandler.OnError);
    }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        IsLoading = true;

        _catalog.Recents()
                .ToListAsync()
                .AsTask()
                .ToObservable()
                .Finally(() => RxApp.MainThreadScheduler.Schedule(() => IsLoading = false))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(list => _torrentsCache.EditDiff(list, (first, second) => first.Link == second.Link), RxApp.DefaultExceptionHandler.OnError);

        return Task.CompletedTask;
    }
}
