using System.Reactive.Concurrency;
using AnitomySharp;
using FuzzySharp;
using Splat;
using Totoro.Core.Helpers;
using Totoro.Core.Services;
using Totoro.Core.Services.MediaEvents;
using Totoro.Core.Torrents;
using TorrentModel = Totoro.Core.Torrents.TorrentModel;

namespace Totoro.Core.ViewModels;

public partial class WatchViewModel : NavigatableViewModel
{
    private readonly ITrackingServiceContext _trackingService;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly IAnimeServiceContext _animeService;
    private readonly ITimestampsService _timestampsService;
    private readonly IStreamPageMapper _streamPageMapper;
    private readonly IDebridServiceContext _debridService;
    private readonly ITorrentCatalog _torrentCatalog;
    private readonly IVideoStreamResolverFactory _videoStreamResolverFactory;
    private readonly List<IMediaEventListener> _mediaEventListeners;
    private readonly ProviderOptions _providerOptions;
    private readonly bool _isCrunchyroll;

    private int? _episodeRequest;
    private double _userSkipOpeningTime;
    private IVideoStreamModelResolver _videoStreamResolver;
    private IAnimeModel _anime;

    public WatchViewModel(IProviderFactory providerFactory,
                          ITrackingServiceContext trackingService,
                          IViewService viewService,
                          ISettings settings,
                          IPlaybackStateStorage playbackStateStorage,
                          IAnimeServiceContext animeService,
                          IMediaPlayer mediaPlayer,
                          ITimestampsService timestampsService,
                          IStreamPageMapper streamPageMapper,
                          IDebridServiceContext debridServiceContext,
                          ITorrentCatalog torrentCatalog,
                          IVideoStreamResolverFactory videoStreamResolverFactory,
                          IEnumerable<IMediaEventListener> mediaEventListeners)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        _settings = settings;
        _playbackStateStorage = playbackStateStorage;
        _animeService = animeService;
        _streamPageMapper = streamPageMapper;
        _debridService = debridServiceContext;
        _torrentCatalog = torrentCatalog;
        _videoStreamResolverFactory = videoStreamResolverFactory;
        _mediaEventListeners = mediaEventListeners.ToList();
        _providerOptions = providerFactory.GetOptions(_settings.DefaultProviderType);
        _isCrunchyroll = _settings.DefaultProviderType == "consumet" && _providerOptions.GetString("Provider", "zoro") == "crunchyroll";
        _timestampsService = timestampsService;

        SetMediaPlayer(mediaPlayer);

        MediaPlayer = mediaPlayer;
        UseDub = !settings.PreferSubs;
        Provider = providerFactory.GetProvider(settings.DefaultProviderType);
        SelectedAudioStream = GetDefaultAudioStream();
        NextEpisode = ReactiveCommand.Create(() => 
        {
            mediaPlayer.Pause(); 
            EpisodeModels.SelectNext();
        }, HasNextEpisode(), RxApp.MainThreadScheduler);
        PrevEpisode = ReactiveCommand.Create(() => 
        {
            mediaPlayer.Pause(); 
            EpisodeModels.SelectPrevious();
        }, HasPrevEpisode(), RxApp.MainThreadScheduler);
        SkipOpening = ReactiveCommand.Create(() =>
        {
            _userSkipOpeningTime = CurrentPlayerTime;
            MediaPlayer.Seek(TimeSpan.FromSeconds(CurrentPlayerTime + settings.OpeningSkipDurationInSeconds));
        }, outputScheduler: RxApp.MainThreadScheduler);
        ChangeQuality = ReactiveCommand.Create<string>(quality =>
        {
            SelectedQuality = null;
            SelectedQuality = quality;
        }, outputScheduler: RxApp.MainThreadScheduler);
        SkipDynamic = ReactiveCommand.Create(() =>
        {
            if (EndingTimeStamp is not null && CurrentPlayerTime > EndingTimeStamp.Interval.StartTime)
            {
                MediaPlayer.Seek(TimeSpan.FromSeconds(EndingTimeStamp.Interval.EndTime));
            }
            else if (OpeningTimeStamp is not null && CurrentPlayerTime > OpeningTimeStamp.Interval.StartTime)
            {
                MediaPlayer.Seek(TimeSpan.FromSeconds(OpeningTimeStamp.Interval.EndTime));
            }
        });
        SubmitTimeStamp = ReactiveCommand.Create(OnSubmitTimeStamps);

        DoStuffOnMediaPlayerEvents();
        DoMainSequence();
        
        MessageBus.Current
            .Listen<RequestFullWindowMessage>()
            .Select(message => message.IsFullWindow)
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.IsFullWindow, deferSubscription: true);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Subscribe(SetAnime);

        this.WhenAnyValue(x => x.SubStreams)
            .Select(x => x.Count() > 1)
            .ToPropertyEx(this, x => x.HasMultipleSubStreams);

        NativeMethods.PreventSleep();
    }

    [Reactive] public bool UseDub { get; set; }
    [Reactive] public bool UseTorrents { get; set; }
    [Reactive] public string SelectedAudioStream { get; set; }
    [Reactive] public int NumberOfEpisodes { get; set; }
    [Reactive] public double CurrentPlayerTime { get; set; }
    [Reactive] public EpisodeModelCollection EpisodeModels { get; set; }
    [Reactive] public IEnumerable<string> Qualities { get; set; }
    [Reactive] public bool HasSubDub { get; set; }
    [Reactive] public IEnumerable<string> SubStreams { get; set; }
    [Reactive] public string SelectedQuality { get; set; }
    [Reactive] public VideoStreamsForEpisodeModel Streams { get; set; }
    [Reactive] public VideoStreamModel SelectedStream { get; set; }


    [ObservableAsProperty] public bool IsFullWindow { get; }
    [ObservableAsProperty] public bool IsSkipButtonVisible { get; }
    [ObservableAsProperty] public double CurrentMediaDuration { get; }
    [ObservableAsProperty] public AniSkipResult AniSkipResult { get; }
    [ObservableAsProperty] public bool HasMultipleSubStreams { get; }

    public IProvider Provider { get; }
    public AniSkipResultItem OpeningTimeStamp { get; set; }
    public AniSkipResultItem EndingTimeStamp { get; set; }
    public IAnimeModel Anime
    {
        get => _anime;
        set => this.RaiseAndSetIfChanged(ref _anime, value);
    }

    public IMediaPlayer MediaPlayer { get; }
    public bool AutoFullScreen => _settings.EnterFullScreenWhenPlaying;
    public TorrentModel Torrent { get; private set; }

    public ICommand NextEpisode { get; }
    public ICommand PrevEpisode { get; }
    public ICommand SkipOpening { get; }
    public ICommand SkipDynamic { get; }
    public ICommand ChangeQuality { get; }
    public ICommand SubmitTimeStamp { get; }

    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
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
            _ = CreateAnimDLResolver(epInfo.Url);
            _ = TrySetAnime(epInfo.Url, epInfo.Title);
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
            _ = CreateAnimDLResolver(searchResult.Url);
            _ = TrySetAnime(searchResult.Url, searchResult.Title);
        }
        else if(parameters.ContainsKey("Torrent"))
        {
            Torrent = (TorrentModel)parameters["Torrent"];
            UseTorrents = true;
            _ = InitializeFromTorrent(Torrent);
        }
    }

    public override Task OnNavigatedFrom()
    {
        NativeMethods.AllowSleep();
        MediaPlayer.Pause();
        return Task.CompletedTask;
    }

    private void DoMainSequence()
    {
        this.ObservableForProperty(x => x.Anime, x => x)
            .WhereNotNull()
            .SelectMany(model => Find(model.Id, model.Title))
            .Where(x => x is not (null, null))
            .Log(this, "Selected Anime", x => $"{x.Sub.Title}")
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                HasSubDub = x is { Dub: { }, Sub: { } };
                var searResult = UseDub ? x.Dub ?? x.Sub : x.Sub;
                _ = CreateAnimDLResolver(searResult.Url);
                if (HasSubDub)
                {
                    this.ObservableForProperty(x => x.UseDub, x => x)
                        .Where(_ => HasSubDub)
                        .Select(useDub => useDub ? x.Dub : x.Sub)
                        .Do(_ => EpisodeModels.Current = null)
                        .Subscribe(x =>
                        {
                            ;
                        }); 
                }
            }, RxApp.DefaultExceptionHandler.OnError);

        this.ObservableForProperty(x => x.NumberOfEpisodes, x => x)
            .Where(epCount => epCount > 0)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(ep => EpisodeModels = EpisodeModelCollection.FromEpisodeCount(ep));

        this.WhenAnyValue(x => x.EpisodeModels)
            .Where(_ => !UseTorrents)
            .WhereNotNull()
            .Select(_ => GetQueuedEpisode())
            .Where(RequestedEpisodeIsValid)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => EpisodeModels.SelectEpisode(x)); // .Subcribe(EpisodeModels.SelecteEpisode) throws exception

        this.WhenAnyValue(x => x.EpisodeModels)
            .WhereNotNull()
            .SelectMany(x => x.WhenAnyValue(x => x.Current))
            .WhereNotNull()
            .Where(ep => ep.EpisodeNumber > 0)
            .Log(this, "Selected Episode :", ep => ep.EpisodeNumber.ToString())
            .Do(epModel =>
            {
                if (CurrentPlayerTime > 60)
                {
                    MediaPlayer.Pause();
                }
                CurrentPlayerTime = 0;
                SetEpisode(epModel.EpisodeNumber);
            })
            .SelectMany(epModel => _videoStreamResolver.Resolve(epModel.EpisodeNumber, SelectedAudioStream))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(stream => Streams = stream);

        this.WhenAnyValue(x => x.Streams)
            .WhereNotNull()
            .Do(stream =>
            {
                SubStreams = stream.StreamTypes;
                Qualities = stream.Qualities;
            })
            .Select(stream => GetDefaultQuality(stream.Qualities))
            .InvokeCommand(ChangeQuality);

        this.WhenAnyValue(x => x.SelectedQuality)
            .WhereNotNull()
            .Subscribe(quality => SelectedStream = Streams.GetStreamModel(quality));

        this.WhenAnyValue(x => x.SelectedStream)
            .WhereNotNull()
            .Subscribe(async stream =>
            {
                if (_torrentCatalog is ISubtitlesDownloader isd)
                {
                    var subtitles = await isd.DownloadSubtitles(Torrent.Link);
                    Streams.AdditionalInformation.Add("subtitleFiles", JsonSerializer.Serialize(subtitles));
                }
                await MediaPlayer.SetMedia(stream, Streams.AdditionalInformation);
                MediaPlayer.Play(GetPlayerTime());
            });
    }


    private void DoStuffOnMediaPlayerEvents()
    {
        MediaPlayer.DisposeWith(Garbage);

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
            .SelectMany(duration => _timestampsService.GetTimeStamps(Anime.Id, EpisodeModels.Current.EpisodeNumber, duration))
            .ToPropertyEx(this, x => x.AniSkipResult, true);

        // Get op and ed in to variable for convenience.
        this.WhenAnyValue(x => x.AniSkipResult)
            .Where(x => x is { Success: true })
            .Subscribe(x =>
            {
                OpeningTimeStamp = x.Opening;
                EndingTimeStamp = x.Ending;
            });
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

    private IObservable<bool> HasNextEpisode()
    {
        return this.WhenAnyValue(x => x.EpisodeModels)
                   .WhereNotNull()
                   .SelectMany(eps => eps.WhenAnyValue(x => x.Current))
                   .WhereNotNull()
                   .Select(x => x != EpisodeModels.Last());
    }

    private IObservable<bool> HasPrevEpisode()
    {
        return this.WhenAnyValue(x => x.EpisodeModels)
                   .WhereNotNull()
                   .SelectMany(eps => eps.WhenAnyValue(x => x.Current))
                   .WhereNotNull()
                   .Select(x => x != EpisodeModels.First());
    }

    private void OnSubmitTimeStamps()
    {
        RxApp.MainThreadScheduler.Schedule(async () =>
        {
            MediaPlayer.Pause();
            await _viewService.SubmitTimeStamp(Anime.Id, EpisodeModels.Current.EpisodeNumber, SelectedStream, AniSkipResult, CurrentMediaDuration, _userSkipOpeningTime == 0 ? 0 : _userSkipOpeningTime - 5);
            MediaPlayer.Play();
        });
    }

    private async Task<(SearchResult Sub, SearchResult Dub)> SearchProvider(string title)
    {
        var results = await Provider.Catalog.Search(title).ToListAsync();

        if (results.Count == 0)
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
        else if (results.FirstOrDefault(x => string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase)) is { } exactMatch)
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
        else if (list.Contains("auto") && _settings.DefaultStreamQualitySelection == StreamQualitySelection.Auto)
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

    private bool RequestedEpisodeIsValid(int episode) => Anime?.TotalEpisodes is 0 or null || episode <= Anime?.TotalEpisodes;

    private int GetQueuedEpisode()
    {
        return _isCrunchyroll
            ? EpisodeModels.Current?.EpisodeNumber ?? 0
            : _episodeRequest ?? EpisodeModels.Current?.EpisodeNumber ?? ((Anime?.Tracking?.WatchedEpisodes ?? 0) + 1);
    }

    private async Task TrySetAnime(string url, string title)
    {
        if (_isCrunchyroll) // crunchyroll combines seasons to single series, had to update tracking 
        {
            return;
        }

        var id = await _streamPageMapper.GetIdFromUrl(url, _settings.DefaultProviderType);

        if (_trackingService.IsAuthenticated)
        {
            id ??= await TryGetId(title);
        }

        if (id is null)
        {
            this.Log().Warn($"Unable to find Id for {title}, watch session will not be tracked");
            return;
        }

        _anime = await _animeService.GetInformation(id.Value);
        SetAnime(_anime);
    }

    private async Task TrySetAnime(string title)
    {
        var id = await TryGetId(title);

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
        if (_settings.DefaultProviderType == "consumet" && _providerOptions?.GetString("Provider", "zoro") == "crunchyroll")
        {
            key = "CrunchyrollStreamType";
        }

        return _providerOptions?.GetString(key, "");
    }

    private double GetPlayerTime()
    {
        if (Anime is null)
        {
            return CurrentPlayerTime;
        }

        return _playbackStateStorage.GetTime(Anime.Id, EpisodeModels.Current.EpisodeNumber);
    }

    private void SetAnime(IAnimeModel anime)
    {
        foreach (var item in _mediaEventListeners)
        {
            item.SetAnime(anime);
        }
    }

    private void SetEpisode(int episode)
    {
        foreach (var item in _mediaEventListeners)
        {
            item.SetCurrentEpisode(episode);
        }
    }

    private void SetSearchResult(SearchResult result)
    {
        foreach (var item in _mediaEventListeners)
        {
            item.SetSearchResult(result);
        }
    }

    private void SetMediaPlayer(IMediaPlayer mediaPlayer)
    {
        foreach (var item in _mediaEventListeners)
        {
            item.SetMediaPlayer(mediaPlayer);
        }
    }

    private async Task InitializeFromTorrent(TorrentModel torrent)
    {
        var parsedResult = AnitomySharp.AnitomySharp.Parse(torrent.Name, new(episode:false, extension:false, group:false));
        if (parsedResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementAnimeTitle) is { } title)
        {
            await TrySetAnime(title.Value);
        }

        var links = (await _debridService.GetDirectDownloadLinks(torrent.MagnetLink)).ToList();

        var options = new Options(title: false, extension: false, group: false);
        foreach (var item in links)
        {
            parsedResult = AnitomySharp.AnitomySharp.Parse(item.FileName, options);
            if (parsedResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeNumber) is { } epString && int.TryParse(epString.Value, out var ep))
            {
                item.Episode = ep;
            }
        }

        _videoStreamResolver = _videoStreamResolverFactory.CreateTorrentStreamResolver(links);
        EpisodeModels = EpisodeModelCollection.FromDirectDownloadLinks(links);

        if(links.Count == 1)
        {
            EpisodeModels.Current = EpisodeModels.First();
        }
    }

    private async Task CreateAnimDLResolver(string url)
    {
        _videoStreamResolver = _videoStreamResolverFactory.CreateAnimDLResolver(url);
        NumberOfEpisodes = await _videoStreamResolver.GetNumberOfEpisodes(SelectedAudioStream);
    }
}