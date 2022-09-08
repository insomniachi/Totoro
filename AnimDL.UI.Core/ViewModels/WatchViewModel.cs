using System.Text.RegularExpressions;
using AnimDL.Api;

namespace AnimDL.UI.Core.ViewModels;

public class WatchViewModel : NavigatableViewModel, IHaveState
{
    private readonly ITrackingService _trackingService;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly IDiscordRichPresense _discordRichPresense;
    private readonly IAnimeService _animeService;
    private readonly ObservableAsPropertyHelper<IProvider> _provider;
    private readonly ObservableAsPropertyHelper<bool> _hasSubAndDub;
    private readonly ObservableAsPropertyHelper<string> _url;
    private ObservableAsPropertyHelper<double> _currentPlayerTime;
    private ObservableAsPropertyHelper<double> _currentMediaDuration;
    private readonly SourceCache<SearchResultModel, string> _searchResultCache = new(x => x.Title);
    private readonly SourceList<int> _episodesCache = new();
    private readonly ReadOnlyObservableCollection<SearchResultModel> _searchResults;
    private readonly ReadOnlyObservableCollection<int> _episodes;

    private int? _episodeRequest;

    public WatchViewModel(IProviderFactory providerFactory,
                          ITrackingService trackingService,
                          IViewService viewService,
                          ISettings settings,
                          IPlaybackStateStorage playbackStateStorage,
                          IDiscordRichPresense discordRichPresense,
                          IAnimeService animeService,
                          IMediaPlayer mediaPlayer)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        _settings = settings;
        _playbackStateStorage = playbackStateStorage;
        _discordRichPresense = discordRichPresense;
        _animeService = animeService;
        MediaPlayer = mediaPlayer;
        SelectedProviderType = _settings.DefaultProviderType;
        UseDub = !settings.PreferSubs;

        _searchResultCache
            .Connect()
            .RefCount()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _searchResults)
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        _episodesCache
            .Connect()
            .RefCount()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _episodes)
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
            .DisposeWith(Garbage);

        SubscribeToMediaPlayerEvents();

        // periodically save the current timestamp so that we can resume later
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(x => Anime is not null && x > 10)
            .Subscribe(time => playbackStateStorage.Update(Anime.Id, CurrentEpisode.Value, time));

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Select(providerFactory.GetProvider)
            .ToProperty(this, nameof(Provider), out _provider, () => providerFactory.GetProvider(ProviderType.AnimixPlay));

        this.WhenAnyValue(x => x.SelectedAnimeResult)
            .Select(x => x is { Dub: { }, Sub: { } })
            .ToProperty(this, nameof(HasSubAndDub), out _hasSubAndDub, () => false);

        /// populate searchbox suggestions
        this.WhenAnyValue(x => x.Query)
            .Where(query => query is { Length: > 3})
            .Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
            .SelectMany(query => animeService.GetAnime(query))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list => _searchResultCache.EditDiff(list, (first, second) => first.Title == second.Title), RxApp.DefaultExceptionHandler.OnNext);

        /// if we have less than configured seconds left and we have not completed this episode
        /// set this episode as watched.
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => Anime is not null)
            .Where(_ => (Anime.Tracking?.WatchedEpisodes ?? 1) <= CurrentEpisode)
            .Where(x => CurrentMediaDuration - x <= settings.TimeRemainingWhenEpisodeCompletes)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(_ => UpdateTracking());

        /// if we have both sub and dub and switch from sub to dub or vice versa
        /// reset <see cref="CurrentEpisode"/> to null, outherwise it won't trigger changed event.
        this.ObservableForProperty(x => x.UseDub, x => x)
            .Where(_ => HasSubAndDub)
            .Select(useDub => useDub ? SelectedAnimeResult.Dub : SelectedAnimeResult.Sub)
            .Do(_ => CurrentEpisode = null)
            .Subscribe(x => SelectedAudio = x);

        /// 1. Select Sub/Dub based in <see cref="UseDub"/> if Dub is not present select Sub
        /// 2. Set <see cref="SelectedAudio"/>
        this.ObservableForProperty(x => x.SelectedAnimeResult, x => x)
            .Select(x => UseDub ? x.Dub ?? x.Sub : x.Sub)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => SelectedAudio = x);

        /// 1. Get the number of Episodes
        /// 2. Populate Episodes list
        /// 3. If we can connect this to a Mal Id, set <see cref="CurrentEpisode"/> to last unwatched ep
        this.ObservableForProperty(x => x.SelectedAudio, x => x)
            .Do(result => DoIfRpcEnabled(() => discordRichPresense.UpdateDetails(result.Title)))
            .SelectMany(result => Provider.StreamProvider.GetNumberOfStreams(result.Url))
            .Select(count => Enumerable.Range(1, count).ToList())
            .Do(list => _episodesCache.EditDiff(list))
            .Select(_ => _episodeRequest ?? (Anime?.Tracking?.WatchedEpisodes + 1) ?? 1)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ep => CurrentEpisode = ep);

        /// Scrape url for <see cref="CurrentEpisode"/> and set to <see cref="Url"/>
        this.ObservableForProperty(x => x.CurrentEpisode, x => x)
            .Where(x => x > 0)
            .Do(x => DoIfRpcEnabled(() => discordRichPresense.UpdateState($"Episode {x}")))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(FetchEpUrl)
            .ToProperty(this, nameof(Url), out _url, () => string.Empty);

        this.ObservableForProperty(x => x.Url, x => x)
            .Where(x => !string.IsNullOrEmpty(x))
            .Do(url => MediaPlayer.SetMediaFromUrl(url))
            .Do(_ => MediaPlayer.Play(_playbackStateStorage.GetTime(Anime?.Id ?? 0, CurrentEpisode ?? 0)))
            .Subscribe();

        this.ObservableForProperty(x => x.Anime, x => x)
            .WhereNotNull()
            .SelectMany(model => Find(model.Id, model.Title))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => SelectedAnimeResult = x);
    }

    [Reactive] public string Query { get; set; }
    [Reactive] public ProviderType SelectedProviderType { get; set; } = ProviderType.AnimixPlay;
    [Reactive] public int? CurrentEpisode { get; set; }
    [Reactive] public bool HideControls { get; set; } = true;
    [Reactive] public string VideoPlayerRequestMessage { get; set; }
    [Reactive] public bool UseDub { get; set; }
    [Reactive] public (SearchResult Sub, SearchResult Dub) SelectedAnimeResult { get; set; }
    [Reactive] public SearchResult SelectedAudio { get; set; }
    [Reactive] public IAnimeModel Anime { get; set; }
    public IProvider Provider => _provider.Value;
    public bool HasSubAndDub => _hasSubAndDub.Value;
    public string Url => _url.Value;
    public double CurrentPlayerTime => _currentPlayerTime?.Value ?? 0;
    public double CurrentMediaDuration => _currentMediaDuration?.Value ?? 0;
    public List<ProviderType> Providers { get; } = Enum.GetValues<ProviderType>().Cast<ProviderType>().ToList();
    public ReadOnlyObservableCollection<int> Episodes => _episodes;
    public ReadOnlyObservableCollection<SearchResultModel> SearchResult => _searchResults;
    public TimeSpan TimeRemaining => TimeSpan.FromSeconds(CurrentMediaDuration - CurrentPlayerTime);
    public IMediaPlayer MediaPlayer { get; }

    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters.ContainsKey("Anime"))
        {
            Anime = parameters["Anime"] as IAnimeModel;
        }
        else if(parameters.ContainsKey("EpisodeInfo"))
        {
            var epInfo = parameters["EpisodeInfo"] as AiredEpisode;
            var epMatch = Regex.Match(epInfo.EpisodeUrl, @"ep(\d+)");
            _episodeRequest = epMatch.Success ? int.Parse(epMatch.Groups[1].Value) : 1;
            Anime = epInfo.Model;
        }
        else if(parameters.ContainsKey("Id"))
        {
            var id = (long)parameters["Id"];
            Anime = await _animeService.GetInformation(id);
        }
        else
        {
            HideControls = false;
        }
    }

    public override Task OnNavigatedFrom()
    {
        if (_settings.UseDiscordRichPresense)
        {
            _discordRichPresense.Clear();
        }

        return Task.CompletedTask;
    }

    public async Task<(SearchResult Sub, SearchResult Dub)> Find(long id, string title)
    {
        if (Provider.Catalog is IMalCatalog malCatalog)
        {
            return await malCatalog.SearchByMalId(id);
        }
        else
        {
            var results = await Provider.Catalog.Search(title).ToListAsync();

            if (results.Count == 1)
            {
                return (results[0], null);
            }
            else if (results.Count == 2)
            {
                return (results[0], results[1]);
            }
            else
            {
                return (await _viewService.ChoooseSearchResult(results, SelectedProviderType), null);
            }
        }
    }

    public Task SetInitialState()
    {
        HideControls = true;
        return Task.CompletedTask;
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(HideControls);

        if (Anime is not null)
        {
            state.AddOrUpdate(Anime);
        }
    }

    public void RestoreState(IState state)
    {
        HideControls = state.GetValue<bool>(nameof(HideControls));
        if (state.GetValue<IAnimeModel>(nameof(Anime)) is IAnimeModel model)
        {
            Anime ??= model;
            HideControls = true;
        }
    }

    private void SubscribeToMediaPlayerEvents()
    {
        MediaPlayer.DisposeWith(Garbage);

        MediaPlayer
            .PositionChanged
            .Select(ts => ts.TotalSeconds)
            .ToProperty(this, nameof(CurrentPlayerTime), out _currentPlayerTime, () => 0.0)
            .DisposeWith(Garbage);

        MediaPlayer
            .DurationChanged
            .Select(ts => ts.TotalSeconds)
            .ToProperty(this, nameof(CurrentMediaDuration), out _currentMediaDuration, () => 0.0)
            .DisposeWith(Garbage);

        MediaPlayer
            .PlaybackEnded
            .Do(_ => DoIfRpcEnabled(() => _discordRichPresense.Clear()))
            .Do(_ => UpdateTracking())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ => CurrentEpisode++)
            .Subscribe().DisposeWith(Garbage);

        MediaPlayer
            .Paused
            .Where(_ => _settings.UseDiscordRichPresense)
            .Do(_ => _discordRichPresense.UpdateDetails("Paused"))
            .Do(_ => _discordRichPresense.UpdateState(""))
            .Do(_ => _discordRichPresense.ClearTimer())
            .Subscribe().DisposeWith(Garbage);

        MediaPlayer
            .Playing
            .Where(_ => _settings.UseDiscordRichPresense)
            .Do(_ => _discordRichPresense.UpdateDetails(SelectedAudio.Title))
            .Do(_ => _discordRichPresense.UpdateState($"Episode {CurrentEpisode}"))
            .Do(_ => _discordRichPresense.UpdateTimer(TimeRemaining))
            .Subscribe().DisposeWith(Garbage);
    }

    private IObservable<string> FetchEpUrl(int? episode)
    {
        return Provider.StreamProvider
                       .GetStreams(SelectedAudio.Url, episode.Value..episode.Value)
                       .ToListAsync().AsTask()
                       .ToObservable()
                       .Select(x => x[0].Qualities.Values.ElementAt(0).Url);
    }

    private void UpdateTracking()
    {
        _playbackStateStorage.Reset(Anime.Id, CurrentEpisode.Value);

        var tracking = new Tracking() { WatchedEpisodes = CurrentEpisode };

        if (CurrentEpisode == Anime.TotalEpisodes)
        {
            tracking.Status = AnimeStatus.Completed;
        }

        _trackingService.Update(Anime.Id, tracking)
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(x => Anime.Tracking = x);
    }

    private void DoIfRpcEnabled(Action action)
    {
        if (!_settings.UseDiscordRichPresense)
        {
            return;
        }

        action();
    }
}