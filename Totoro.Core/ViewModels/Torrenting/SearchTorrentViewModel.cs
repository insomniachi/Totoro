using System.Reactive.Concurrency;
using Flurl.Util;
using Totoro.Plugins;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Torrents.Contracts;
using Totoro.Plugins.Torrents.Models;

namespace Totoro.Core.ViewModels.Torrenting;

public class SearchTorrentViewModel : NavigatableViewModel, IHaveState
{
    private readonly IPluginFactory<ITorrentTracker> _indexerFactory;
    private readonly ISettings _settings;
    private readonly IDebridServiceContext _debridServiceContext;
    private readonly SourceCache<TorrentModel, string> _torrentsCache = new(x => x.Link);
    private readonly ReadOnlyObservableCollection<TorrentModel> _torrents;

    public SearchTorrentViewModel(IPluginFactory<ITorrentTracker> indexerFactory,
                                  ISettings settings,
                                  IDebridServiceContext debridServiceContext)
    {
        _indexerFactory = indexerFactory;
        _settings = settings;
        _debridServiceContext = debridServiceContext;

        Plugins = indexerFactory.Plugins.ToList();
        IsDebridAuthenticated = debridServiceContext.IsAuthenticated;
        Search = ReactiveCommand.Create(OnSearch);

        var sort = this.WhenAnyValue(x => x.SortMode)
            .WhereNotNull()
            .Select(sort => sort switch
            {
                SortMode.Date => SortExpressionComparer<TorrentModel>.Descending(x => x.Date),
                SortMode.Seeders => SortExpressionComparer<TorrentModel>.Descending(x => x.Seeders),
                _ => SortExpressionComparer<TorrentModel>.Ascending(x => _torrentsCache.Items.IndexOf(x))
            });
        
        _torrentsCache
            .Connect()
            .RefCount()
            .Sort(sort)
            .Bind(out _torrents)
            .Subscribe()
            .DisposeWith(Garbage);

        this.WhenAnyValue(x => x.PastedTorrent.Magnet)
            .Where(x => !string.IsNullOrEmpty(x))
            .Subscribe(_ => PastedTorrent.State = TorrentState.Cached);

        this.WhenAnyValue(x => x.SelectedPlugin)
            .WhereNotNull()
            .Select(plugin => indexerFactory.CreatePlugin(plugin.Name))
            .ToPropertyEx(this, x => x.Catalog);

        this.WhenAnyValue(x => x.Catalog)
            .WhereNotNull()
            .Do(_ => _torrentsCache.Clear())
            .Subscribe(_ =>
            {
                if (!string.IsNullOrEmpty(Query))
                {
                    OnSearch();
                }
                else
                {
                    OnRecent();
                }
            });
    }

    [Reactive] public string Query { get; set; }
    [Reactive] public SortMode SortMode { get; set; } = SortMode.None;
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public PluginInfo SelectedPlugin { get; set; }
    [ObservableAsProperty] public ITorrentTracker Catalog { get; }


    public List<PluginInfo> Plugins { get; }
    public ReadOnlyObservableCollection<TorrentModel> Torrents => _torrents;
    public TorrentModel PastedTorrent { get; } = new();
    public bool IsDebridAuthenticated { get; }

    public ICommand Search { get; }


    public Task SetInitialState()
    {
        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(_torrentsCache.Items, nameof(Torrents));
        state.AddOrUpdate(SortMode);
        state.AddOrUpdate(Query);
        state.AddOrUpdate(SelectedPlugin);
    }

    public void RestoreState(IState state)
    {
        var torrents = state.GetValue<IEnumerable<TorrentModel>>(nameof(Torrents));
        _torrentsCache.AddOrUpdate(torrents);
        SortMode = state.GetValue<SortMode>(nameof(SortMode));
        Query = state.GetValue<string>(nameof(Query));
        SelectedPlugin = state.GetValue<PluginInfo>(nameof(SelectedPlugin));
    }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        var indexer = (string)parameters.GetValueOrDefault("Indexer", _settings.DefaultTorrentTrackerType);

        if (parameters.ContainsKey("Anime"))
        {
            var anime = (AnimeModel)parameters["Anime"];
            Query = GetQueryText(anime);
        }

        SelectedPlugin = Plugins.FirstOrDefault(x => x.Name == indexer);
        
        return Task.CompletedTask;
    }

    private void OnRecent()
    {
        IsLoading = true;

        Catalog.Recents()
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

    private void OnSearch()
    {
        IsLoading = true;

        Catalog?.Search(Query)
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

    private async Task UpdateCachedState(List<TorrentModel> torrents)
    {
        var index = 0;
        await foreach (var item in _debridServiceContext.Check(torrents.Select(x => x.Magnet)))
        {
            torrents[index++].State = item ? TorrentState.Cached : TorrentState.NotCached;
        }
    }
}
