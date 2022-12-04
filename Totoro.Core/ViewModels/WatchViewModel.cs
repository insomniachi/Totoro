using System.Text.RegularExpressions;
using AnimDL.Api;
using Splat;
using Totoro.Core.Helpers;
using Totoro.Core.Services;

namespace Totoro.Core.ViewModels;

public partial class WatchViewModel : NavigatableViewModel, IHaveState
{
    private readonly ITrackingService _trackingService;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly IDiscordRichPresense _discordRichPresense;
    private readonly IAnimeService _animeService;
    private readonly IRecentEpisodesProvider _recentEpisodesProvider;
    private readonly ObservableAsPropertyHelper<IProvider> _provider;
    private readonly ObservableAsPropertyHelper<bool> _hasSubAndDub;
    private readonly ObservableAsPropertyHelper<VideoStreamsForEpisode> _streams;
    private readonly ObservableAsPropertyHelper<IEnumerable<string>> _qualities;
    private readonly ObservableAsPropertyHelper<double> _introPostion;
    private readonly ObservableAsPropertyHelper<double> _outroPosition;
    private readonly ObservableAsPropertyHelper<TimeSpan> _introEndPosition;
    private readonly ObservableAsPropertyHelper<bool> _isSkipIntroButtonVisible;
    private readonly ObservableAsPropertyHelper<AnimeTimeStamps> _timeStamps;
    private readonly ObservableAsPropertyHelper<double> _currentPlayerTime;
    private readonly ObservableAsPropertyHelper<double> _currentMediaDuration;
    private readonly ObservableAsPropertyHelper<bool> _isFullWindow;
    private readonly SourceCache<SearchResultModel, string> _searchResultCache = new(x => x.Title);
    private readonly SourceList<int> _episodesCache = new();
    private readonly ReadOnlyObservableCollection<SearchResultModel> _searchResults;
    private readonly ReadOnlyObservableCollection<int> _episodes;

    private int? _episodeRequest;
    private bool _canUpdateTime = false;
    private bool _isUpdatingTracking = false;

    public WatchViewModel(IProviderFactory providerFactory,
                          ITrackingService trackingService,
                          IViewService viewService,
                          ISettings settings,
                          IPlaybackStateStorage playbackStateStorage,
                          IDiscordRichPresense discordRichPresense,
                          IAnimeService animeService,
                          IMediaPlayer mediaPlayer,
                          ITimestampsService timestampsService,
                          IRecentEpisodesProvider recentEpisodesProvider,
                          ILocalMediaService localMediaService)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        _settings = settings;
        _playbackStateStorage = playbackStateStorage;
        _discordRichPresense = discordRichPresense;
        _animeService = animeService;
        _recentEpisodesProvider = recentEpisodesProvider;

        MediaPlayer = mediaPlayer;
        SelectedProviderType = _settings.DefaultProviderType;
        UseDub = !settings.PreferSubs;

        NextEpisode = ReactiveCommand.Create(() => { _canUpdateTime = false; mediaPlayer.Pause(); ++CurrentEpisode; }, HasNextEpisode, RxApp.MainThreadScheduler);
        PrevEpisode = ReactiveCommand.Create(() => { _canUpdateTime = false; mediaPlayer.Pause(); --CurrentEpisode; }, HasPrevEpisode, RxApp.MainThreadScheduler);
        SkipOpening = ReactiveCommand.Create(() => MediaPlayer.Seek(TimeSpan.FromSeconds(CurrentPlayerTime + settings.OpeningSkipDurationInSeconds)), outputScheduler: RxApp.MainThreadScheduler);
        ChangeQuality = ReactiveCommand.Create<string>(quality => SelectedStream = Streams.Qualities[quality], outputScheduler: RxApp.MainThreadScheduler);
        SkipOpeningDynamic = ReactiveCommand.Create(() => MediaPlayer.Seek(IntroEndPosition), this.WhenAnyValue(x => x.IntroEndPosition).Select(x => x.TotalSeconds > 0), RxApp.MainThreadScheduler);

        var episodeChanged = this.ObservableForProperty(x => x.CurrentEpisode, x => x)
            .Where(ep => ep > 0);

        SubscribeToMediaPlayerEvents();

        _searchResultCache
            .Connect()
            .RefCount()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _searchResults)
            .Subscribe()
            .DisposeWith(Garbage);

        _episodesCache
            .Connect()
            .RefCount()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _episodes)
            .Subscribe()
            .DisposeWith(Garbage);

        MessageBus.Current
            .Listen<RequestFullWindowMessage>()
            .Select(message => message.IsFullWindow)
            .ToProperty(this, nameof(IsFullWindow), out _isFullWindow, () => false);

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Select(providerFactory.GetProvider)
            .ToProperty(this, nameof(Provider), out _provider, () => providerFactory.GetProvider(SelectedProviderType));

        this.WhenAnyValue(x => x.SelectedAnimeResult)
            .Select(x => x is { Dub: { }, Sub: { } })
            .ToProperty(this, nameof(HasSubAndDub), out _hasSubAndDub, () => false);

        // periodically save the current timestamp so that we can resume later
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(x => Anime is not null && x > 10)
            .Where(x => _canUpdateTime)
            .Subscribe(time => playbackStateStorage.Update(Anime.Id, CurrentEpisode.Value, time));

        // if we actualy know when episode ends, update tracking then.
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(x => OutroPosition > 0 && x >= OutroPosition && Anime is not null && (Anime.Tracking?.WatchedEpisodes ?? 1) < CurrentEpisode)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => UpdateTracking())
            .Subscribe();

        /// if we have less than configured seconds left and we have not completed this episode
        /// set this episode as watched.
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => Anime is not null && OutroPosition <= 0)
            .Where(_ => (Anime.Tracking?.WatchedEpisodes ?? 0) < CurrentEpisode)
            .Where(x => CurrentMediaDuration - x <= settings.TimeRemainingWhenEpisodeCompletesInSeconds)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => UpdateTracking())
            .Subscribe();

        /// populate searchbox suggestions
        this.WhenAnyValue(x => x.Query)
            .Where(query => query is { Length: > 3 })
            .Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
            .SelectMany(query => animeService.GetAnime(query))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(list => _searchResultCache.EditDiff(list, (first, second) => first.Title == second.Title), RxApp.DefaultExceptionHandler.OnNext);

        // Triggers the first step to scrape stream urls
        this.ObservableForProperty(x => x.Anime, x => x)
            .Where(_ => !UseLocalMedia)
            .WhereNotNull()
            .SelectMany(model => Find(model.Id, model.Title))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => SelectedAnimeResult = x);

        this.ObservableForProperty(x => x.Anime, x => x)
            .Where(_ => UseLocalMedia)
            .WhereNotNull()
            .Select(model => localMediaService.GetEpisodes(model.Id))
            .Do(eps => _episodesCache.EditDiff(eps))
            .Select(_ => _episodeRequest ?? Anime?.Tracking?.WatchedEpisodes + 1 ?? 1)
            .Where(ep => ep <= Anime?.TotalEpisodes)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ep => CurrentEpisode = ep);

        /// 1. Select Sub/Dub based in <see cref="UseDub"/> if Dub is not present select Sub
        /// 2. Set <see cref="SelectedAudio"/>
        this.ObservableForProperty(x => x.SelectedAnimeResult, x => x)
            .Select(x => UseDub ? x.Dub ?? x.Sub : x.Sub)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => SelectedAudio = x);

        /// if we have both sub and dub and switch from sub to dub or vice versa
        /// reset <see cref="CurrentEpisode"/> to null, outherwise it won't trigger changed event.
        this.ObservableForProperty(x => x.UseDub, x => x)
            .Where(_ => HasSubAndDub)
            .Select(useDub => useDub ? SelectedAnimeResult.Dub : SelectedAnimeResult.Sub)
            .Do(_ => CurrentEpisode = null)
            .Subscribe(x => SelectedAudio = x);

        /// 1. Get the number of Episodes
        /// 2. Populate Episodes list
        /// 3. If we can connect this to a Mal Id, set <see cref="CurrentEpisode"/> to last unwatched ep
        this.ObservableForProperty(x => x.SelectedAudio, x => x)
            .Do(result => DoIfRpcEnabled(() => discordRichPresense.UpdateDetails(result.Title)))
            .SelectMany(result => Provider.StreamProvider.GetNumberOfStreams(result.Url))
            .Select(count => Enumerable.Range(1, count).ToList())
            .Do(list => _episodesCache.EditDiff(list))
            .Select(_ => _episodeRequest ?? (Anime?.Tracking?.WatchedEpisodes ?? 0) + 1)
            .Where(ep => ep <= (Anime?.TotalEpisodes ?? int.MaxValue))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ep => CurrentEpisode = ep);

        /// Scrape url for <see cref="CurrentEpisode"/> and set to <see cref="Url"/>
        episodeChanged
            .Where(_ => !UseLocalMedia)
            .Do(x => DoIfRpcEnabled(() => discordRichPresense.UpdateState($"Episode {x}")))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(ep => Provider.StreamProvider.GetStreams(SelectedAudio.Url, ep.Value..ep.Value).ToListAsync().AsTask())
            .Select(list => list.FirstOrDefault())
            .WhereNotNull()
            .ToProperty(this, nameof(Streams), out _streams);

        episodeChanged
            .Where(_ => UseLocalMedia)
            .Do(x => DoIfRpcEnabled(() => discordRichPresense.UpdateState($"Episode {x}")))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Select(ep => localMediaService.GetMedia(Anime.Id, ep.Value))
            .Where(file => !string.IsNullOrEmpty(file))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(async file => await mediaPlayer.SetMedia(file))
            .Do(_ => mediaPlayer.Play(playbackStateStorage.GetTime(Anime?.Id ?? 0, CurrentEpisode ?? 0)))
            .Subscribe();

        // Update qualities selection
        this.ObservableForProperty(x => x.Streams, x => x)
            .WhereNotNull()
            .Select(x => x.Qualities.Keys)
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToProperty(this, nameof(Qualities), out _qualities, () => Enumerable.Empty<string>());

        // Start playing when we can and start from the previous session if exists
        this.ObservableForProperty(x => x.SelectedStream, x => x)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(stream => mediaPlayer.SetMedia(stream))
            .Do(_ => mediaPlayer.Play(playbackStateStorage.GetTime(Anime?.Id ?? 0, CurrentEpisode ?? 0)))
            .Subscribe();

        // get start position of intro if available
        episodeChanged
            .Select(ep => ep.ToString())
            .Select(ep => TimeStamps?.GetIntroStartPosition(ep) ?? 0)
            .ToProperty(this, nameof(IntroPosition), out _introPostion);

        // get end position of intro if available
        episodeChanged
            .Select(ep => ep.ToString())
            .Select(ep => TimeStamps?.GetIntroEndPosition(ep) ?? TimeSpan.FromSeconds(0))
            .ToProperty(this, nameof(IntroEndPosition), out _introEndPosition);

        // get start position of outro if available
        episodeChanged
            .Select(ep => ep.ToString())
            .Select(ep => TimeStamps?.GetOutroStartPosition(ep) ?? 0)
            .ToProperty(this, nameof(OutroPosition), out _outroPosition);

        SkipButtonVisibleTrigger()            .Merge(SkipButtonHideTrigger())
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToProperty(this, nameof(IsSkipIntroButtonVisible), out _isSkipIntroButtonVisible)
            .DisposeWith(Garbage);

        // Fetch timestamps if exists
        this.ObservableForProperty(x => x.Anime, x => x)
            .WhereNotNull()
            .Select(x => x.Id)
            .SelectMany(timestampsService.GetTimeStamps)
            .ToProperty(this, nameof(TimeStamps), out _timeStamps);

        MediaPlayer
            .PositionChanged
            .Select(ts => ts.TotalSeconds)
            .ToProperty(this, nameof(CurrentPlayerTime), out _currentPlayerTime)
            .DisposeWith(Garbage);

        MediaPlayer
            .DurationChanged
            .Select(ts => ts.TotalSeconds)
            .ToProperty(this, nameof(CurrentMediaDuration), out _currentMediaDuration)
            .DisposeWith(Garbage);
    }

    [Reactive] public string Query { get; set; }
    [Reactive] public ProviderType SelectedProviderType { get; set; } = ProviderType.AnimixPlay;
    [Reactive] public int? CurrentEpisode { get; set; }
    [Reactive] public bool HideControls { get; set; } = true;
    [Reactive] public bool UseDub { get; set; }
    [Reactive] public (SearchResult Sub, SearchResult Dub) SelectedAnimeResult { get; set; }
    [Reactive] public SearchResult SelectedAudio { get; set; }
    [Reactive] public AnimeModel Anime { get; set; }
    [Reactive] public VideoStream SelectedStream { get; set; }
    [Reactive] public bool UseLocalMedia { get; set; }
    public bool IsFullWindow => _isFullWindow?.Value ?? false;
    public bool IsSkipIntroButtonVisible => _isSkipIntroButtonVisible?.Value ?? false;
    public IProvider Provider => _provider.Value;
    public bool HasSubAndDub => _hasSubAndDub.Value;
    public double CurrentPlayerTime => _currentPlayerTime?.Value ?? 0;
    public double CurrentMediaDuration => _currentMediaDuration?.Value ?? 0;
    public double IntroPosition => _introPostion?.Value ?? 0;
    public double OutroPosition => _outroPosition?.Value ?? 0;
    public TimeSpan IntroEndPosition => _introEndPosition?.Value ?? TimeSpan.FromSeconds(0);
    public AnimeTimeStamps TimeStamps => _timeStamps?.Value ?? new();
    public List<ProviderType> Providers { get; } = Enum.GetValues<ProviderType>().Cast<ProviderType>().ToList();
    public ReadOnlyObservableCollection<int> Episodes => _episodes;
    public ReadOnlyObservableCollection<SearchResultModel> SearchResult => _searchResults;
    public VideoStreamsForEpisode Streams => _streams.Value;
    public IEnumerable<string> Qualities => _qualities.Value;
    public TimeSpan TimeRemaining => TimeSpan.FromSeconds(CurrentMediaDuration - CurrentPlayerTime);
    public IMediaPlayer MediaPlayer { get; }

    public ICommand NextEpisode { get; }
    public ICommand PrevEpisode { get; }
    public ICommand SkipOpening { get; }
    public ICommand SkipOpeningDynamic { get; }
    public ICommand ChangeQuality { get; }
    
    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if(parameters.ContainsKey("UseLocalMedia"))
        {
            UseLocalMedia = (bool)parameters["UseLocalMedia"];
        }

        if (parameters.ContainsKey("Anime"))
        {
            Anime = parameters["Anime"] as AnimeModel;
        }
        else if (parameters.ContainsKey("EpisodeInfo"))
        {
            var epInfo = parameters["EpisodeInfo"] as AiredEpisode;
            var epMatch = EpisodeRegex().Match(epInfo.EpisodeUrl);
            _episodeRequest = epMatch.Success ? int.Parse(epMatch.Groups[1].Value) : 1;

            _recentEpisodesProvider
                .GetMalId(epInfo)
                .Where(id => id > 0)
                .SelectMany(_animeService.GetInformation)
                .Subscribe(x => Anime = x)
                .DisposeWith(Garbage);
        }
        else if (parameters.ContainsKey("Id"))
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

        NativeMethods.AllowSleep();

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
        //if (state.GetValue<IAnimeModel>(nameof(Anime)) is IAnimeModel model)
        //{
        //    Anime ??= model;
        //    HideControls = true;
        //}
    }

    private void SubscribeToMediaPlayerEvents()
    {
        MediaPlayer.DisposeWith(Garbage);


        MediaPlayer
            .PlaybackEnded
            .Do(_ => _canUpdateTime = false)
            .Do(_ => DoIfRpcEnabled(() => _discordRichPresense.Clear()))
            .SelectMany(_ => UpdateTracking())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(_ => NativeMethods.AllowSleep())
            .InvokeCommand(NextEpisode)
            .DisposeWith(Garbage);

        MediaPlayer
            .Paused
            .Where(_ => _settings.UseDiscordRichPresense)
            .Do(_ => _discordRichPresense.UpdateState($"Episode {CurrentEpisode} (Paused)"))
            .Do(_ => _discordRichPresense.ClearTimer())
            .Do(_ => NativeMethods.AllowSleep())
            .Subscribe().DisposeWith(Garbage);

        MediaPlayer
            .Playing
            .Where(_ => _settings.UseDiscordRichPresense)
            .Do(_ => _canUpdateTime = true)
            .Do(_ => _discordRichPresense.UpdateDetails(SelectedAudio?.Title ?? Anime.Title))
            .Do(_ => _discordRichPresense.UpdateImage(Anime.Image))
            .Do(_ => _discordRichPresense.UpdateState($"Episode {CurrentEpisode}"))
            .Do(_ => _discordRichPresense.UpdateTimer(TimeRemaining))
            .Do(_ => NativeMethods.PreventSleep())
            .Subscribe().DisposeWith(Garbage);
    }

    public async Task<Unit> UpdateTracking()
    {
        if(_isUpdatingTracking || Anime.Tracking is not null && Anime.Tracking.WatchedEpisodes >= Streams.Episode)
        {
            return Unit.Default;
        }

        _isUpdatingTracking = true;
        this.Log().Debug($"Updating tracking for {Anime.Title} from {Anime.Tracking?.WatchedEpisodes ?? Streams.Episode - 1} to {Streams.Episode}");

        _playbackStateStorage.Reset(Anime.Id, Streams.Episode);

        var tracking = new Tracking() { WatchedEpisodes = Streams.Episode };

        if (Streams.Episode == Anime.TotalEpisodes)
        {
            tracking.Status = AnimeStatus.Completed;
            tracking.FinishDate = DateTime.Today;
        }
        else if(Streams.Episode == 1)
        {
            tracking.Status = AnimeStatus.Watching;
            tracking.StartDate = DateTime.Today;
        }

        Anime.Tracking = await _trackingService.Update(Anime.Id, tracking);

        _isUpdatingTracking = false;

        return Unit.Default;
    }

    private void DoIfRpcEnabled(Action action)
    {
        if (!_settings.UseDiscordRichPresense)
        {
            return;
        }

        action();
    }

    private IObservable<bool> SkipButtonVisibleTrigger()
    {
        return this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
                   .Where(_ => !IsSkipIntroButtonVisible && IntroPosition > 0)
                   .Where(x => x >= IntroPosition && x <= IntroEndPosition.TotalSeconds)
                   .Select(_ => true);
    }

    private IObservable<bool> SkipButtonHideTrigger()
    {
        return this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
                   .Where(_ => IsSkipIntroButtonVisible && IntroEndPosition.TotalSeconds > 0)
                   .Where(x => x >= IntroEndPosition.TotalSeconds || x <= IntroPosition)
                   .Select(_ => false);
    }

    private IObservable<bool> HasNextEpisode => this.ObservableForProperty(x => x.CurrentEpisode, x => x).Select(episode => episode != Episodes.LastOrDefault());
    private IObservable<bool> HasPrevEpisode => this.ObservableForProperty(x => x.CurrentEpisode, x => x).Select(episode => episode != Episodes.FirstOrDefault());

    [GeneratedRegex("ep(\\d+)")]
    private static partial Regex EpisodeRegex();
}