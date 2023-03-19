using System.Reactive.Concurrency;
using Totoro.Core.Services.Debrid;
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
    private readonly IAnimeIdService _animeIdService;
    private readonly SourceCache<TorrentModel, string> _torrentsCache = new(x => x.Link);
    private readonly SourceCache<Transfer, string> _transfersCache = new(x => x.Name);
    private readonly ReadOnlyObservableCollection<TorrentModel> _torrents;
    private readonly ReadOnlyObservableCollection<Transfer> _transfers;
    private IDisposable _transfersSubscription;
    private bool _isSubscriptionDisposed;
    private static readonly string _webTorrentsCliFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules", "webtorrent-cli");

    public TorrentingViewModel(IDebridServiceContext debridServiceContext,
                               ITorrentCatalogFactory catalogFactory,
                               IAnimeIdService animeIdService,
                               ISettings settings)
    {
        _debridServiceContext = debridServiceContext;
        _catalog = catalogFactory.GetCatalog(settings.TorrentProviderType);
        _animeIdService = animeIdService;

        CanUseTorrents = _debridServiceContext.IsAuthenticated || Directory.Exists(_webTorrentsCliFolder);
        ProviderType = settings.TorrentProviderType;
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

        _transfersCache
            .Connect()
            .RefCount()
            .Bind(out _transfers)
            .Subscribe()
            .DisposeWith(Garbage);

        _debridServiceContext
            .TransferCreated
            .Subscribe(_ =>
            {
                if (!_isSubscriptionDisposed)
                {
                    return;
                }

                MonitorTransfers();
            })
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.PastedTorrent.MagnetLink)
            .Where(x => !string.IsNullOrEmpty(x))
            .Subscribe(_ => PastedTorrent.State = TorrentState.Unknown);

        Search = ReactiveCommand.Create(OnSearch);
    }

    [Reactive] public string Query { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public SortMode SortMode { get; set; } = SortMode.Seeders;
    
    public TorrentProviderType ProviderType { get; }
    public TorrentModel PastedTorrent { get; } = new();
    public bool CanUseTorrents { get; }
    public ReadOnlyObservableCollection<TorrentModel> Torrents => _torrents;
    public ReadOnlyObservableCollection<Transfer> Transfers => _transfers;

    public ICommand Search { get; }

    private void MonitorTransfers()
    {
        _transfersSubscription = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(1))
            .SelectMany(_ => _debridServiceContext.GetTransfers())
            .Select(list => list.Where(x => x.Status != "finished"))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list =>
            {
                _transfersCache.Clear();
                
                if (!list.Any())
                {
                    DisposeSubscription();
                    return;
                }

                _transfersCache.AddOrUpdate(list);

            }, RxApp.DefaultExceptionHandler.OnError);

        _isSubscriptionDisposed = false;
    }

    private void DisposeSubscription()
    {
        _transfersSubscription?.Dispose();
        _isSubscriptionDisposed = true;
    }

    
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

    public override Task OnNavigatedFrom()
    {
        DisposeSubscription();
        return Task.CompletedTask;
    }

    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if(!CanUseTorrents)
        {
            return;
        }

        IsLoading = true;
        MonitorTransfers();


        if (parameters.ContainsKey("Anime"))
        {
            SortMode = SortMode.Seeders;
            var anime = (AnimeModel)parameters["Anime"];
            Query = GetQueryText(anime);
            if (_catalog is IIndexedTorrentCatalog itc && anime is not null)
            {
                var id = await _animeIdService.GetId(anime.Id);
                itc.Search(Query, id)
                   .ToListAsync()
                   .AsTask()
                   .ToObservable()
                   .Finally(() => RxApp.MainThreadScheduler.Schedule(() => IsLoading = false))
                   .ObserveOn(RxApp.MainThreadScheduler)
                   .Subscribe(list => _torrentsCache.EditDiff(list, (first, second) => first.Link == second.Link), RxApp.DefaultExceptionHandler.OnError);
            }
            else
            {
                OnSearch();
            }
        }
        else
        {
            _catalog.Recents()
                    .ToListAsync()
                    .AsTask()
                    .ToObservable()
                    .Finally(() => RxApp.MainThreadScheduler.Schedule(() => IsLoading = false))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(list => _torrentsCache.EditDiff(list, (first, second) => first.Link == second.Link), RxApp.DefaultExceptionHandler.OnError);
        }
    }

    private static string GetQueryText(AnimeModel anime)
    {
        var watchedEpisodes = anime.Tracking?.WatchedEpisodes ?? 0;

        if(watchedEpisodes == anime.AiredEpisodes)
        {
            return anime.Title;
        }

        return $"{anime.Title} - {(watchedEpisodes + 1).ToString().PadLeft(2, '0')}";
    }
}
