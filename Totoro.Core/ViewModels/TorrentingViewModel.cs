using System.Reactive.Concurrency;
using Totoro.Core.Services.Debrid;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Torrents.Contracts;
using Totoro.Plugins.Torrents.Models;

namespace Totoro.Core.ViewModels;

public enum SortMode
{
    Date,
    Seeders
}

public class TorrentingViewModel : NavigatableViewModel
{
    private readonly IDebridServiceContext _debridServiceContext;
    private readonly IPluginFactory<ITorrentTracker> _indexerFactory;
    private readonly IAnimeIdService _animeIdService;
    private readonly ISettings _settings;
    private readonly SourceCache<TorrentModel, string> _torrentsCache = new(x => x.Link);
    private readonly SourceCache<Transfer, string> _transfersCache = new(x => x.Name);
    private readonly ReadOnlyObservableCollection<TorrentModel> _torrents;
    private readonly ReadOnlyObservableCollection<Transfer> _transfers;
    private ITorrentTracker _catalog;
    private IDisposable _transfersSubscription;
    private bool _isSubscriptionDisposed;

    public TorrentingViewModel(IDebridServiceContext debridServiceContext,
                               IPluginFactory<ITorrentTracker> indexerFactory,
                               IAnimeIdService animeIdService,
                               ISettings settings,
                               ITorrentEngine torrentEngine)
    {
        _debridServiceContext = debridServiceContext;
        _indexerFactory = indexerFactory;
        _animeIdService = animeIdService;
        _settings = settings;

        IsDebridAuthenticated = _debridServiceContext.IsAuthenticated;
        
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

        torrentEngine
            .TorrentRemoved
            .Select(name => EngineTorrents.FirstOrDefault(x => x.Name == name))
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                EngineTorrents.Remove(x);
                x.Dispose();
            });

        torrentEngine
            .TorrentAdded
            .Do(_ => SelectedSection = Sections.First(x => x.Header == "Downloads"))
            .Subscribe(x => EngineTorrents.Add(new TorrentManagerModel(torrentEngine, x)));

        Search = ReactiveCommand.Create(OnSearch);

        if(IsDebridAuthenticated)
        {
            Sections.Add(new PivotItemModel { Header = "Transfers" });
        }

        EngineTorrents = new(torrentEngine.TorrentManagers.Select(x => new TorrentManagerModel(torrentEngine, x)));
        
        this.WhenAnyValue(x => x.PastedTorrent.Magnet)
            .Where(x => !string.IsNullOrEmpty(x))
            .Subscribe(_ => PastedTorrent.State = TorrentState.Unknown);

    }

    [Reactive] public string Query { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public SortMode SortMode { get; set; } = SortMode.Seeders;
    [Reactive] public PivotItemModel SelectedSection { get; set; }
    public bool IsDebridAuthenticated { get; }

    [Reactive] public string ProviderType { get; private set; }
    public TorrentModel PastedTorrent { get; } = new();
    public ReadOnlyObservableCollection<TorrentModel> Torrents => _torrents;
    public ReadOnlyObservableCollection<Transfer> Transfers => _transfers;
    public ObservableCollection<PivotItemModel> Sections { get; } = new()
    {
        new PivotItemModel{ Header = "Torrents" },
        new PivotItemModel{ Header = "Downloads" }
    };

    public ObservableCollection<TorrentManagerModel> EngineTorrents { get; }

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
                .Subscribe(async list =>
                {
                    await UpdateCachedState(list);
                    _torrentsCache.EditDiff(list, (first, second) => first.Link == second.Link);

                }, RxApp.DefaultExceptionHandler.OnError);
    }

    public override Task OnNavigatedFrom()
    {
        DisposeSubscription();
        return Task.CompletedTask;
    }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        IsLoading = true;
        MonitorTransfers();

        var indexer = (string)parameters.GetValueOrDefault("Indexer", _settings.DefaultTorrentTrackerType);
        _catalog = _indexerFactory.CreatePlugin(indexer);
        ProviderType = indexer;

        if (parameters.ContainsKey("Anime"))
        {
            SortMode = SortMode.Seeders;
            var anime = (AnimeModel)parameters["Anime"];
            Query = GetQueryText(anime);
            OnSearch();
        }
        else
        {
            _catalog.Recents()
                    .ToListAsync()
                    .AsTask()
                    .ToObservable()
                    .Finally(() => RxApp.MainThreadScheduler.Schedule(() => IsLoading = false))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(async list =>
                    {
                        await UpdateCachedState(list);
                        _torrentsCache.EditDiff(list, (first, second) => first.Link == second.Link);
                    }, RxApp.DefaultExceptionHandler.OnError);
        }

        return Task.CompletedTask;
    }

    private async Task UpdateCachedState(List<TorrentModel> torrents)
    {
        var index = 0;
        await foreach (var item in _debridServiceContext.Check(torrents.Select(x => x.Magnet)))
        {
            torrents[index++].State = item ? TorrentState.Cached : TorrentState.NotCached;
        }
    }

    private string GetQueryText(AnimeModel anime)
    {
        var watchedEpisodes = anime.Tracking?.WatchedEpisodes ?? 0;
        var nextEpisode = (watchedEpisodes + 1).ToString().PadLeft(2, '0');

        if (_settings.TorrentSearchOptions.IsEnabled)
        {
            if (watchedEpisodes == anime.AiredEpisodes)
            {
                return $"[{_settings.TorrentSearchOptions.Subber}] {anime.Title} {_settings.TorrentSearchOptions.Quality}";
            }

            return $"[{_settings.TorrentSearchOptions.Subber}] {anime.Title} - {nextEpisode} {_settings.TorrentSearchOptions.Quality}";
        }
        else
        {

            if (watchedEpisodes == anime.AiredEpisodes)
            {
                return anime.Title;
            }

            return $"{anime.Title} - {nextEpisode}";
        }
    }
}
