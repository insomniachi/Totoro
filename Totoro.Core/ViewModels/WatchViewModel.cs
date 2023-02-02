using System.Reactive.Concurrency;
using AnimDL.Core.Models.Interfaces;
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
    private readonly ITimestampsService _timestampsService;
    private readonly ILocalMediaService _localMediaService;
    private readonly IStreamPageMapper _streamPageMapper;
    private readonly SourceList<int> _episodesCache = new();
    private readonly ReadOnlyObservableCollection<int> _episodes;
    private readonly ProviderOptions _providerOptions;
    private readonly bool _isCrunchyroll;
   
    private int? _episodeRequest;
    private bool _canUpdateTime = false;
    private bool _isUpdatingTracking = false;
    private double _userSkipOpeningTime;

    private IAnimeModel _anime;

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
        _providerOptions = providerFactory.GetOptions(_settings.DefaultProviderType);
        _isCrunchyroll = _settings.DefaultProviderType == "consumet" && _providerOptions.GetString("Provider", "zoro") == "crunchyroll";
        _timestampsService = timestampsService;
        _localMediaService = localMediaService;
        _episodesCache
            .Connect()
            .RefCount()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _episodes)
            .Subscribe()
            .DisposeWith(Garbage);


        MediaPlayer = mediaPlayer;
        UseDub = !settings.PreferSubs;
        Provider = providerFactory.GetProvider(settings.DefaultProviderType);
        SelectedAudioStream = GetDefaultAudioStream();
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

        DoStuffOnMediaPlayerEvents();
        DoMainSequence();
        DoStuffOnUserInteraction();

        MessageBus.Current
            .Listen<RequestFullWindowMessage>()
            .Select(message => message.IsFullWindow)
            .ToPropertyEx(this, x => x.IsFullWindow, deferSubscription: true);

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

        NativeMethods.PreventSleep();
    }

    [Reactive] public int? CurrentEpisode { get; set; }
    [Reactive] public bool UseDub { get; set; }
    [Reactive] public (SearchResult Sub, SearchResult Dub) SelectedAnimeResult { get; set; }
    [Reactive] public SearchResult SelectedAudio { get; set; }
    [Reactive] public VideoStream SelectedStream { get; set; }
    [Reactive] public bool UseLocalMedia { get; set; }
    [Reactive] public VideoStreamsForEpisode Streams { get; set; }
    [Reactive] public string SelectedAudioStream { get; set; }
    [Reactive] public int NumberOfStreams { get; set; }
    [Reactive] public double CurrentPlayerTime { get; set; }
    
    [ObservableAsProperty] public bool IsFullWindow { get; }
    [ObservableAsProperty] public bool IsSkipButtonVisible { get; }
    [ObservableAsProperty] public bool HasSubAndDub { get; }
    [ObservableAsProperty] public double CurrentMediaDuration { get; }
    [ObservableAsProperty] public IEnumerable<string> Qualities { get; }
    [ObservableAsProperty] public AniSkipResult AniSkipResult { get; }
    [ObservableAsProperty] public bool HasMultipleAudioStreams { get; }
    [ObservableAsProperty] public IEnumerable<string> AudioStreams { get; }

    public IProvider Provider { get; }
    public AniSkipResultItem OpeningTimeStamp { get; set; }
    public AniSkipResultItem EndingTimeStamp { get; set; }
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
            // Start Main Sequence from Step 1
            Anime = parameters["Anime"] as IAnimeModel;
        }
        else if (parameters.ContainsKey("EpisodeInfo"))
        {
            // Start Main Sequence from Step 1.1 but plays this episode instead of the next unwatched episode
            var epInfo = parameters["EpisodeInfo"] as AiredEpisode;
            _episodeRequest = epInfo.Episode;
            await TrySetAnime(epInfo.Url, epInfo.Title);
            SelectedAudio = new SearchResult { Title = epInfo.Title, Url = epInfo.Url };
        }
        else if (parameters.ContainsKey("Id"))
        {
            // Start Main Sequence from Step 1
            var id = (long)parameters["Id"];
            Anime = await _animeService.GetInformation(id);
        }
        else if (parameters.ContainsKey("SearchResult"))
        {
            // Start Main Sequence from Step 1.1
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

    private void DoStuffOnUserInteraction()
    {
        DoStuffWhenEpisodeChanges();

        // TODO: can we be removed safely ?
        DoStuffBasedOnSubDubToggle();

        DoStuffWhenLanguageOrSeasonChange();
    }

    private void DoFetchLanguagesOrSeasonSequence()
    {
        // Step 5.1
        this.WhenAnyValue(x => x.Streams)
            .WhereNotNull()
            .Select(x => x.StreamTypes.Any())
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.HasMultipleAudioStreams);

        // Step 5.2
        this.WhenAnyValue(x => x.HasMultipleAudioStreams)
            .Where(x => x)
            .Select(_ => Streams.StreamTypes.Order())
            .Log(this, "Stream Types", x => string.Join(",", x))
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.AudioStreams);

        // Step 5.3
        this.WhenAnyValue(x => x.AudioStreams)
            .WhereNotNull()
            .Where(Enumerable.Any)
            .Select(_ => GetDefaultAudioStream())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => SelectedAudioStream = x);

        /// Step 5.4 <see cref="DoStuffWhenLanguageOrSeasonChange"/>
    }

    private void DoStuffWhenLanguageOrSeasonChange()
    {
        // Language changed, (also for season changed in case of crunchyroll)
        // new sequence starts from Step 3.
        this.ObservableForProperty(x => x.SelectedAudioStream, x => x)
            .WhereNotNull()
            .Log(this, "Selected Audio Stream")
            .SelectMany(_ => GetNumberOfStreams(SelectedAudio.Url))
            .Subscribe(count =>
            {
                if(NumberOfStreams == count)
                {
                    NumberOfStreams = -1;
                }
                NumberOfStreams = count;
            });
    }

    private void DoStuffWhenEpisodeChanges()
    {
        var episodeChanged = this.ObservableForProperty(x => x.CurrentEpisode, x => x)
            .Where(ep => ep > 0)
            .Do(_ =>
            {
                MediaPlayer.Pause();
                CurrentPlayerTime = 0;
            });

        // when streaming
        episodeChanged
            .Where(_ => !UseLocalMedia)
            .Do(x => DoIfRpcEnabled(() => _discordRichPresense.UpdateState($"Episode {x}")))
            .Log(this, "Current Episode")
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(ep => GetStreams())
            .Select(list => list.FirstOrDefault())
            .WhereNotNull()
            .Subscribe(x => Streams = x);

        // when playing files in system
        episodeChanged
            .Where(_ => UseLocalMedia)
            .Do(x => DoIfRpcEnabled(() => _discordRichPresense.UpdateState($"Episode {x}")))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Select(ep => _localMediaService.GetMedia(Anime.Id, ep.Value))
            .Where(file => !string.IsNullOrEmpty(file))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(async file => await MediaPlayer.SetMedia(file))
            .Do(_ => MediaPlayer.Play(_playbackStateStorage.GetTime(Anime?.Id ?? 0, CurrentEpisode ?? 0)))
            .Subscribe();
    }

    private void DoMainSequence()
    {
        /// Step 1   -> set <see cref="SelectedAnimeResult"/> from default provider based on Anime from MAL/Anilist etc...
        /// Step 1.1 -> set <see cref="SelectedAudio"/> based in <see cref="UseDub"/> (this should be removed later)
        /// Step 2   -> Get the number of episodes available and set <see cref="NumberOfStreams"/>
        /// Step 3   -> a) Populate Episodes list (<see cref="Episodes"/>)
        ///             b) If already watching, select next unwatched episode or select the first <see cref="GetQueuedEpisode"/> as <see cref="CurrentEpisode"/>
        /// Step 4   -> a) Update discord rich presence if enabled
        ///             b) Get streams for current episode and set <see cref="Streams"/>
        /// Step 5   -> a) Get and set available stream qualities <see cref="Qualities"/> for the default lanugage (subbed or subbed season 1 in case of CR)
        ///             b) Start subsequence <see cref="DoFetchLanguagesOrSeasonSequence"/>
        /// Step 6   -> a) Select Default quality <see cref="GetDefaultQuality(IEnumerable{string})"/>
        ///             b) set <see cref="SelectedStream"/> which corresponds to the quality
        /// Step 7   -> a) Set the source in MediaPlayer <see cref="IMediaPlayer.SetMedia(VideoStream, Dictionary{string, string})"/>
        ///             b) if episode is partially watched, resume from where we left, otherwise start from the beginning


        // Step 1
        this.ObservableForProperty(x => x.Anime, x => x)
            .Where(_ => !UseLocalMedia)
            .WhereNotNull()
            .SelectMany(model => Find(model.Id, model.Title))
            .Where(x => x is not (null, null))
            .Log(this, "Selected Anime", x => $"{x.Sub.Title}")
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => SelectedAnimeResult = x);

        // Step 2
        this.ObservableForProperty(x => x.SelectedAudio, x => x)
            .Do(result => DoIfRpcEnabled(() => _discordRichPresense.UpdateDetails(result.Title)))
            .Select(x => x.Url)
            .SelectMany(GetNumberOfStreams)
            .Subscribe(count =>
            {
                if (NumberOfStreams == count)
                {
                    NumberOfStreams = -1;
                }
                NumberOfStreams = count;
            });

        // Step 3
        this.ObservableForProperty(x => x.NumberOfStreams, x => x)
            .Where(count => count > 0)
            .Log(this, "Number of Episodes")
            .Select(count => Enumerable.Range(1, count).ToList())
            .Do(list => _episodesCache.EditDiff(list))
            .Select(_ => GetQueuedEpisode())
            .Log(this, "Episode Queued")
            .Where(RequestedEpisodeIsValid)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ep =>
            {
                if (CurrentEpisode == ep)
                {
                    CurrentEpisode = null;
                }

                CurrentEpisode = ep;
            });

        /// Step 4 <see cref="DoStuffWhenEpisodeChanges"/>

        // Step 5
        this.ObservableForProperty(x => x.Streams, x => x)
            .WhereNotNull()
            .Select(FormatQualityStrings)
            .Log(this, "Qualities", JoinStrings)
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.Qualities, Enumerable.Empty<string>(), true);

        // Step 5a Subsequence
        DoFetchLanguagesOrSeasonSequence();

        // Step 6
        this.WhenAnyValue(x => x.Qualities)
            .Where(Enumerable.Any)
            .Select(GetDefaultQuality)
            .Log(this, "Selected Quality")
            .InvokeCommand(ChangeQuality);

        // Step 7
        this.ObservableForProperty(x => x.SelectedStream, x => x)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Log(this, "Stream changed", x => x.Url)
            .Subscribe(async stream =>
            {
                await MediaPlayer.SetMedia(stream, Streams.AdditionalInformation);
                MediaPlayer.Play(GetPlayerTime());
            });


    }

    private void DoStuffBasedOnSubDubToggle()
    {
        this.WhenAnyValue(x => x.SelectedAnimeResult)
            .Select(x => x is { Dub: { }, Sub: { } })
            .ToPropertyEx(this, x => x.HasSubAndDub, deferSubscription: true);

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
    }

    private void DoStuffOnMediaPlayerEvents()
    {
        MediaPlayer.DisposeWith(Garbage);

        MediaPlayer
            .PositionChanged
            .Select(ts => ts.TotalSeconds)
            .Subscribe(x => CurrentPlayerTime = x)
            .DisposeWith(Garbage);

        MediaPlayer
            .DurationChanged
            .Select(ts => ts.TotalSeconds)
            .ToPropertyEx(this, x => x.CurrentMediaDuration, true)
            .DisposeWith(Garbage);

        MediaPlayer
            .PlaybackEnded
            .Do(_ =>
            {
                _canUpdateTime = false;
                DoIfRpcEnabled(() => _discordRichPresense.Clear());
            })
            .SelectMany(_ => UpdateTracking())
            .ObserveOn(RxApp.MainThreadScheduler)
            .InvokeCommand(NextEpisode)
            .DisposeWith(Garbage);

        MediaPlayer
            .Paused
            .Where(_ => _settings.UseDiscordRichPresense)
            .Subscribe(_ =>
            {
                _discordRichPresense.UpdateState($"Episode {CurrentEpisode} (Paused)");
                _discordRichPresense.ClearTimer();
            })
            .DisposeWith(Garbage);

        MediaPlayer
            .Playing
            .Where(_ => _settings.UseDiscordRichPresense)
            .Subscribe(_ =>
            {
                _canUpdateTime = true;
                _discordRichPresense.UpdateDetails(SelectedAudio?.Title ?? Anime.Title);
                _discordRichPresense.UpdateState($"Episode {CurrentEpisode}");
                _discordRichPresense.UpdateTimer(TimeRemaining);
                _discordRichPresense.UpdateImage(GetDiscordImageKey());
            })
            .DisposeWith(Garbage);

        // display skip button based on current time
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => AniSkipResult is { Success: true })
            .Select(IsPlayingOpeningOrEnding)
            .DistinctUntilChanged()
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.IsSkipButtonVisible, deferSubscription: true);

        // when total duration of episode received, try to get skip times
        this.ObservableForProperty(x => x.CurrentMediaDuration, x => x)
            .Where(_ => Anime is not null)
            .Where(duration => duration > 0)
            .Throttle(TimeSpan.FromSeconds(1))
            .SelectMany(duration => _timestampsService.GetTimeStamps(Anime.Id, CurrentEpisode!.Value, duration))
            .ToPropertyEx(this, x => x.AniSkipResult, true);

        // Get op and ed in to variable for convenience.
        this.WhenAnyValue(x => x.AniSkipResult)
            .Where(x => x is { Success: true })
            .Subscribe(x =>
            {
                OpeningTimeStamp = x.Opening;
                EndingTimeStamp = x.Ending;
            });

        // periodically save the current timestamp so that we can resume later
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(x => Anime is not null && x > 10 && _canUpdateTime)
            .Subscribe(time => _playbackStateStorage.Update(Anime.Id, CurrentEpisode.Value, time));


        // if we actualy know when episode ends, update tracking then.
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => Anime is not null && EndingTimeStamp is not null)
            .Where(x => x >= EndingTimeStamp.Interval.StartTime && (Anime.Tracking?.WatchedEpisodes ?? 1) < CurrentEpisode)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => UpdateTracking())
            .Subscribe();

        /// if we have less than configured seconds left and we have not completed this episode
        /// set this episode as watched.
        this.ObservableForProperty(x => x.CurrentPlayerTime, x => x)
            .Where(_ => Anime is not null && EndingTimeStamp is null)
            .Where(_ => (Anime.Tracking?.WatchedEpisodes ?? 0) < CurrentEpisode)
            .Where(x => CurrentMediaDuration - x <= _settings.TimeRemainingWhenEpisodeCompletesInSeconds)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(_ => UpdateTracking())
            .Subscribe();
    }

    private async Task<(SearchResult Sub, SearchResult Dub)> Find(long id, string title)
    {
        if (Provider is null)
        {
            return (null, null);
        }

        if (Provider.Catalog is IMalCatalog malCatalog)
        {
            return await malCatalog.SearchByMalId(id);
        }
        return await _streamPageMapper.GetStreamPage(id, _settings.DefaultProviderType) ?? await SearchProvider(title);
    }

    public async Task<Unit> UpdateTracking()
    {
        if(!CanUpdateTracking())
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
            return (await _viewService.ChoooseSearchResult(suggested, results, _settings.DefaultProviderType), null);
        }
    }

    /// <summary>
    /// If only 1 quality available select that
    /// If Auto qualities is available select that
    /// else select highest quality possible.
    /// <remarks>'hardsub' is filtered away because it only comes from crunchyroll and crunchyroll provides softsubs which is better</remarks>
    /// </summary>
    /// <param name="qualities"></param>
    /// <returns></returns>
    private string GetDefaultQuality(IEnumerable<string> qualities)
    {
        var list = qualities.ToList();

        if (list.Count == 1)
        {
            return list.First();
        }
        else if(list.Contains("auto") && _settings.DefaultStreamQualitySelection == StreamQualitySelection.Auto)
        {
            return "auto";
        }
        else
        {
            return list.Where(x => x != "hardsub").OrderBy(x => x.Length).ThenBy(x => x).LastOrDefault();
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
    
    private int GetQueuedEpisode()
    {
        return _isCrunchyroll 
            ? CurrentEpisode ?? 0
            : _episodeRequest ?? CurrentEpisode ?? ((Anime?.Tracking?.WatchedEpisodes ?? 0) + 1);
    }
    
    private async Task TrySetAnime(string url, string title)
    {
        if(_isCrunchyroll) // crunchyroll combines seasons to single series, had to update tracking 
        {
            return;
        }

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

    private string GetDefaultAudioStream()
    {
        var key = "StreamType";
        if(_settings.DefaultProviderType == "consumet" && _providerOptions?.GetString("Provider", "zoro") == "crunchyroll")
        {
            key = "CrunchyrollStreamType";
        }

        return _providerOptions?.GetString(key, "");
    }

    private string GetDiscordImageKey()
    {
        if(SelectedAudio is IHaveImage ihi)
        {
            return ihi.Image;
        }

        return "icon";
    }

    private Task<int> GetNumberOfStreams(string url)
    {
        return Provider?.StreamProvider switch
        {
            IMultiAudioStreamProvider mp => mp.GetNumberOfStreams(url, SelectedAudioStream),
            IStreamProvider sp => sp.GetNumberOfStreams(url),
            _ => Task.FromResult(0)
        };
    }

    private Task<List<VideoStreamsForEpisode>> GetStreams()
    {
        return Provider?.StreamProvider switch
        {
            IMultiAudioStreamProvider mp => mp.GetStreams(SelectedAudio.Url, GetRange(), SelectedAudioStream).ToListAsync().AsTask(),
            IStreamProvider sp => sp.GetStreams(SelectedAudio.Url, GetRange()).ToListAsync().AsTask(),
            _ => Task.FromResult(new List<VideoStreamsForEpisode>())
        };
    }

    private Range GetRange() => CurrentEpisode.Value..CurrentEpisode.Value;

    private bool CanUpdateTracking()
    {
        if (_isCrunchyroll || // crunchyroll combines seasons to single series, had to update tracking 
            Anime is null || // we don't know which anime to update
            _isUpdatingTracking || // already updating is in progress
            Anime.Tracking is not null && Anime.Tracking.WatchedEpisodes >= Streams.Episode) // rewatching an episode
        {
            return false;
        }

        return true;
    }

    private double GetPlayerTime()
    {
        if(Anime is null)
        {
            return CurrentPlayerTime;
        }

        return _playbackStateStorage.GetTime(Anime.Id, CurrentEpisode ?? 0);
    }
}