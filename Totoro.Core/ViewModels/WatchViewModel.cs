using System.Reactive.Concurrency;
using FuzzySharp;
using Splat;
using Totoro.Core.Helpers;

namespace Totoro.Core.ViewModels;

public partial class WatchViewModel : NavigatableViewModel
{
    private readonly ITrackingServiceContext _trackingService;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly IDiscordRichPresense _discordRichPresense;
    private readonly IAnimeServiceContext _animeService;
    private readonly IStreamPageMapper _streamPageMapper;
    private readonly SourceList<int> _episodesCache = new();
    private readonly ReadOnlyObservableCollection<int> _episodes;

    private int? _episodeRequest;
    private bool _canUpdateTime = false;
    private bool _isUpdatingTracking = false;
    private double _userSkipOpeningTime;

    public WatchViewModel(IProviderFactory providerFactory,
                          ITrackingServiceContext trackingService,
                          IViewService viewService,
                          ISettings settings,
                          IPlaybackStateStorage playbackStateStorage,
                          IDiscordRichPresense discordRichPresense,
                          IAnimeServiceContext animeService,
                          IMediaPlayer mediaPlayer,
                          ITimestampsService timestampsService,
                          ILocalMediaService localMediaService,
                          IStreamPageMapper streamPageMapper)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        _settings = settings;
        _playbackStateStorage = playbackStateStorage;
        _discordRichPresense = discordRichPresense;
        _animeService = animeService;
        _streamPageMapper = streamPageMapper;

        MediaPlayer = mediaPlayer;
        SelectedProviderType = _settings.DefaultProviderType;
        UseDub = !settings.PreferSubs;

        NextEpisode = ReactiveCommand.Create(() => { _canUpdateTime = false; mediaPlayer.Pause(); ++CurrentEpisode; }, HasNextEpisode, RxApp.MainThreadScheduler);
        PrevEpisode = ReactiveCommand.Create(() => { _canUpdateTime = false; mediaPlayer.Pause(); --CurrentEpisode; }, HasPrevEpisode, RxApp.MainThreadScheduler);
        SkipOpening = ReactiveCommand.Create(() =>
        {
            _userSkipOpeningTime = CurrentPlayerTime;
            MediaPlayer.Seek(TimeSpan.FromSeconds(CurrentPlayerTime + settings.OpeningSkipDurationInSeconds));
        }, outputScheduler: RxApp.MainThreadScheduler);
        ChangeQuality = ReactiveCommand.Create<string>(quality => SelectedStream = Streams.Qualities[quality], outputScheduler: RxApp.MainThreadScheduler);
        SkipDynamic = ReactiveCommand.Create(() =>
        {
            if(EndingTimeStamp is not null && CurrentPlayerTime > EndingTimeStamp.Interval.StartTime)
            {
                MediaPlayer.Seek(TimeSpan.FromSeconds(EndingTimeStamp.Interval.EndTime));
            }
            else if(OpeningTimeStamp is not null && CurrentPlayerTime > OpeningTimeStamp.Interval.StartTime)
            {
                MediaPlayer.Seek(TimeSpan.FromSeconds(OpeningTimeStamp.Interval.EndTime));
            }
        });
        SubmitTimeStamp = ReactiveCommand.Create(OnSubmitTimeStamps);

        var episodeChanged = this.ObservableForProperty(x => x.CurrentEpisode, x => x)
            .Where(ep => ep > 0);

        SubscribeToMediaPlayerEvents();

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
            .ToPropertyEx(this, x => x.IsFullWindow, deferSubscription: true);

        this.WhenAnyValue(x => x.SelectedProviderType)
            .Select(providerFactory.GetProvider)
            .ToPropertyEx(this, x => x.Provider, providerFactory.GetProvider(SelectedProviderType), true);

        this.WhenAnyValue(x => x.SelectedAnimeResult)
            .Select(x => x is { Dub: { }, Sub: { } })
            .ToPropertyEx(this, x => x.HasSubAndDub, deferSubscription: true);

        // periodically save the current timestamp so that we can resume later
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(x => Anime is not null && x > 10 && _canUpdateTime)
            .Subscribe(time => playbackStateStorage.Update(Anime.Id, CurrentEpisode.Value, time));

        // if we actualy know when episode ends, update tracking then.
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => Anime is not null && EndingTimeStamp is not null)
            .Where(x => x >= EndingTimeStamp.Interval.StartTime  && (Anime.Tracking?.WatchedEpisodes ?? 1) < CurrentEpisode)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => UpdateTracking())
            .Subscribe();

        /// if we have less than configured seconds left and we have not completed this episode
        /// set this episode as watched.
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => Anime is not null && EndingTimeStamp is null)
            .Where(_ => (Anime.Tracking?.WatchedEpisodes ?? 0) < CurrentEpisode)
            .Where(x => CurrentMediaDuration - x <= settings.TimeRemainingWhenEpisodeCompletesInSeconds)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => UpdateTracking())
            .Subscribe();

        // Triggers the first step to scrape stream urls
        this.ObservableForProperty(x => x.Anime, x => x)
            .Where(_ => !UseLocalMedia)
            .WhereNotNull()
            .SelectMany(model => Find(model.Id, model.Title))
            .Where(x => x is not (null, null))
            .Log(this, "Selected Anime", x => $"{x.Sub.Title}")
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => SelectedAnimeResult = x);

        this.ObservableForProperty(x => x.Anime, x => x)
            .Where(_ => UseLocalMedia)
            .WhereNotNull()
            .Select(model => localMediaService.GetEpisodes(model.Id))
            .Do(eps => _episodesCache.EditDiff(eps))
            .Select(_ => GetQueuedEpisode())
            .Log(this, "Episode Queued")
            .Where(RequestedEpisodeIsValid)
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
            .Log(this, "Number of Episodes")
            .Select(count => Enumerable.Range(1, count).ToList())
            .Do(list => _episodesCache.EditDiff(list))
            .Select(_ => GetQueuedEpisode())
            .Log(this, "Episode Queued")
            .Where(RequestedEpisodeIsValid)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ep => CurrentEpisode = ep);

        /// Scrape url for <see cref="CurrentEpisode"/> and set to <see cref="Url"/>
        episodeChanged
            .Where(_ => !UseLocalMedia)
            .Do(x => DoIfRpcEnabled(() => discordRichPresense.UpdateState($"Episode {x}")))
            .Log(this, "Current Episode")
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(ep => Provider.StreamProvider.GetStreams(SelectedAudio.Url, ep.Value..ep.Value).ToListAsync().AsTask())
            .Select(list => list.FirstOrDefault())
            .WhereNotNull()
            .ToPropertyEx(this, x => x.Streams, true);

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
            .Select(FormatQualityStrings)
            .Log(this, "Qualities", JoinStrings)
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.Qualities, Enumerable.Empty<string>(), true);

        // Start playing when we can and start from the previous session if exists
        this.ObservableForProperty(x => x.SelectedStream, x => x)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Log(this, "Stream changed", x => x.Url)
            .Do(mediaPlayer.SetMedia)
            .Do(_ => mediaPlayer.Play(playbackStateStorage.GetTime(Anime?.Id ?? 0, CurrentEpisode ?? 0)))
            .Subscribe();

        MediaPlayer
            .PositionChanged
            .Select(ts => ts.TotalSeconds)
            .ToPropertyEx(this, x => x.CurrentPlayerTime, true)
            .DisposeWith(Garbage);

        MediaPlayer
            .DurationChanged
            .Select(ts => ts.TotalSeconds)
            .ToPropertyEx(this, x => x.CurrentMediaDuration, true)
            .DisposeWith(Garbage);

        this.ObservableForProperty(x => x.CurrentMediaDuration, x => x)
            .Where(_ => Anime is not null)
            .Where(duration => duration > 0)
            .Throttle(TimeSpan.FromSeconds(1))
            .SelectMany(duration => timestampsService.GetTimeStamps(Anime.Id, CurrentEpisode!.Value, duration))
            .ToPropertyEx(this, x => x.AniSkipResult, true);

        this.WhenAnyValue(x => x.AniSkipResult)
            .Where(x => x is { Success: true })
            .Subscribe(x =>
            {
                OpeningTimeStamp = x.Opening;
                EndingTimeStamp = x.Ending;
            });

        this.WhenAnyValue(x => x.Qualities)
            .Where(Enumerable.Any)
            .Select(GetDefaultQuality)
            .Log(this, "Selected Quality")
            .InvokeCommand(ChangeQuality);

        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => AniSkipResult is { Success: true })
            .Select(IsPlayingOpeningOrEnding)
            .DistinctUntilChanged()
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.IsSkipButtonVisible, deferSubscription: true);

    }

    [Reactive] public string SelectedProviderType { get; set; } = "allanime";
    [Reactive] public int? CurrentEpisode { get; set; }
    [Reactive] public bool UseDub { get; set; }
    [Reactive] public (SearchResult Sub, SearchResult Dub) SelectedAnimeResult { get; set; }
    [Reactive] public SearchResult SelectedAudio { get; set; }
    [Reactive] public VideoStream SelectedStream { get; set; }
    [Reactive] public bool UseLocalMedia { get; set; }
    [ObservableAsProperty] public bool IsFullWindow { get; }
    [ObservableAsProperty] public bool IsSkipButtonVisible { get; }
    [ObservableAsProperty] public IProvider Provider { get; }
    [ObservableAsProperty] public bool HasSubAndDub { get; }
    [ObservableAsProperty] public double CurrentPlayerTime { get; }
    [ObservableAsProperty] public double CurrentMediaDuration { get; }
    [ObservableAsProperty] public VideoStreamsForEpisode Streams { get; }
    [ObservableAsProperty] public IEnumerable<string> Qualities { get; }
    [ObservableAsProperty] public AniSkipResult AniSkipResult { get; }
    public AniSkipResultItem OpeningTimeStamp { get; set; }
    public AniSkipResultItem EndingTimeStamp { get; set; }

    private IAnimeModel _anime;
    public IAnimeModel Anime
    {
        get => _anime;
        set => this.RaiseAndSetIfChanged(ref _anime, value);
    }

    public ReadOnlyObservableCollection<int> Episodes => _episodes;
    public TimeSpan TimeRemaining => TimeSpan.FromSeconds(CurrentMediaDuration - CurrentPlayerTime);
    public IMediaPlayer MediaPlayer { get; }

    public ICommand NextEpisode { get; }
    public ICommand PrevEpisode { get; }
    public ICommand SkipOpening { get; }
    public ICommand SkipDynamic { get; }
    public ICommand ChangeQuality { get; }
    public ICommand SubmitTimeStamp { get; }

    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters.ContainsKey("UseLocalMedia"))
        {
            UseLocalMedia = (bool)parameters["UseLocalMedia"];
        }

        if (parameters.ContainsKey("Anime"))
        {
            Anime = parameters["Anime"] as IAnimeModel;
        }
        else if (parameters.ContainsKey("EpisodeInfo"))
        {
            var epInfo = parameters["EpisodeInfo"] as AiredEpisode;
            _episodeRequest = epInfo.Episode;
            await TrySetAnime(epInfo.Url, epInfo.Title);
            SelectedAudio = new SearchResult { Title = epInfo.Title, Url = epInfo.Url };
        }
        else if (parameters.ContainsKey("Id"))
        {
            var id = (long)parameters["Id"];
            Anime = await _animeService.GetInformation(id);
        }
        else if (parameters.ContainsKey("SearchResult"))
        {
            var searchResult = (SearchResult)parameters["SearchResult"];
            await TrySetAnime(searchResult.Url, searchResult.Title);
            SelectedAudio = searchResult;
        }
    }

    public override Task OnNavigatedFrom()
    {
        if (_settings.UseDiscordRichPresense)
        {
            _discordRichPresense.Clear();
        }

        NativeMethods.AllowSleep();
        MediaPlayer.Pause();
        _playbackStateStorage.StoreState();
        return Task.CompletedTask;
    }

    public async Task<(SearchResult Sub, SearchResult Dub)> Find(long id, string title)
    {
        if (Provider.Catalog is IMalCatalog malCatalog)
        {
            return await malCatalog.SearchByMalId(id);
        }
        return await _streamPageMapper.GetStreamPage(id, _settings.DefaultProviderType) ?? await SearchProvider(title);
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
            .Do(_ => _discordRichPresense.UpdateState($"Episode {CurrentEpisode}"))
            .Do(_ => _discordRichPresense.UpdateTimer(TimeRemaining))
            .Do(_ => NativeMethods.PreventSleep())
            .Subscribe().DisposeWith(Garbage);
    }

    public async Task<Unit> UpdateTracking()
    {
        if(_settings.DefaultProviderType == "kamy") // kamy combines seasons to single series, had to update tracking 
        {
            return Unit.Default;
        }

        if(Anime is null)
        {
            return Unit.Default;
        }

        if (_isUpdatingTracking || Anime.Tracking is not null && Anime.Tracking.WatchedEpisodes >= Streams.Episode)
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
        else if (Streams.Episode == 1)
        {
            tracking.Status = AnimeStatus.Watching;
            tracking.StartDate = DateTime.Today;
        }

        Anime.Tracking = await _trackingService.Update(Anime.Id, tracking);

        _isUpdatingTracking = false;

        if (_settings.ContributeTimeStamps && AniSkipResult?.Items.Any(x => x.SkipType == "op") is not true) // don't force to submit if only ed is missing
        {
            OnSubmitTimeStamps();
        }

        _playbackStateStorage.Reset(Anime.Id, CurrentEpisode ?? 0);

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

    private IObservable<bool> HasNextEpisode => this.ObservableForProperty(x => x.CurrentEpisode, x => x).Select(episode => episode != Episodes.LastOrDefault());
    private IObservable<bool> HasPrevEpisode => this.ObservableForProperty(x => x.CurrentEpisode, x => x).Select(episode => episode != Episodes.FirstOrDefault());

    private void OnSubmitTimeStamps()
    {
        RxApp.MainThreadScheduler.Schedule(async () =>
        {
            MediaPlayer.Pause();
            await _viewService.SubmitTimeStamp(Anime.Id, CurrentEpisode.Value, SelectedStream, AniSkipResult, CurrentMediaDuration, _userSkipOpeningTime == 0 ? 0 : _userSkipOpeningTime - 5);
            MediaPlayer.Play();
        });
    }

    private async Task<(SearchResult Sub, SearchResult Dub)> SearchProvider(string title)
    {
        var results = await Provider.Catalog.Search(title).ToListAsync();

        if(results.Count == 0)
        {
            return (null, null);
        }

        if (results.Count == 1)
        {
            return (results[0], null);
        }
        else if (results.Count == 2 && _settings.DefaultProviderType is "gogo") // gogo anime has separate listing for sub/dub
        {
            return (results[0], results[1]);
        }
        else if(results.FirstOrDefault(x => string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase)) is { } exactMatch)
        {
            return (exactMatch, null);
        }
        else
        {
            var suggested = results.MaxBy(x => Fuzz.PartialRatio(x.Title, title));
            this.Log().Debug($"{results.Count} entries found, suggested entry : {suggested.Title}({suggested.Url}) Confidence : {Fuzz.PartialRatio(suggested.Title, title)}");
            return (await _viewService.ChoooseSearchResult(suggested, results, SelectedProviderType), null);
        }
    }

    private static string GetDefaultQuality(IEnumerable<string> qualities)
    {
        var list = qualities.ToList();

        if (list.Count == 1)
        {
            return list.First();
        }
        else
        {
            return list.OrderBy(x => x.Length).ThenBy(x => x).LastOrDefault();
        }
    }
    private static IEnumerable<string> FormatQualityStrings(VideoStreamsForEpisode stream)
    {
        return stream.Qualities
                     .Select(x => x.Key)
                     .OrderBy(x => x.Length)
                     .ThenBy(x => x);
    }
    private static string JoinStrings(IEnumerable<string> items) => string.Join(",", items);
    private bool RequestedEpisodeIsValid(int episode) => Anime?.TotalEpisodes is 0 or null || episode <= Anime?.TotalEpisodes;
    private int GetQueuedEpisode() => _episodeRequest ?? ((Anime?.Tracking?.WatchedEpisodes ?? 0) + 1);
    private async Task TrySetAnime(string url, string title)
    {
        var id = await _streamPageMapper.GetIdFromUrl(url, _settings.DefaultProviderType);

        if(_trackingService.IsAuthenticated)
        {
            id ??= await TryGetId(title);
        }

        if (id is null)
        {
            this.Log().Warn($"Unable to find Id for {title}, watch session will not be tracked");
            return;
        }

        _anime = await _animeService.GetInformation(id.Value);
    }

    private async Task<long?> TryGetId(string title)
    {
        if (_settings.DefaultProviderType == "kamy") // kamy combines seasons to single series, had to update tracking 
        {
            return 0;
        }

        return await _viewService.TryGetId(title);
    }
    private bool IsPlayingOpeningOrEnding(double currentTime)
    {
        var isOpening = OpeningTimeStamp is not null && currentTime > OpeningTimeStamp.Interval.StartTime && currentTime < OpeningTimeStamp.Interval.EndTime;
        var isEnding = EndingTimeStamp is not null && currentTime > EndingTimeStamp.Interval.StartTime && currentTime < EndingTimeStamp.Interval.EndTime;

        return isOpening || isEnding;   
    }
}