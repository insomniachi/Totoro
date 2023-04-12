using System.Reactive.Concurrency;
using AnitomySharp;
using FuzzySharp;
using Humanizer;
using Splat;
using Totoro.Core.Helpers;
using Totoro.Core.Services.MediaEvents;
using TorrentModel = Totoro.Core.Torrents.TorrentModel;

namespace Totoro.Core.ViewModels;

public partial class WatchViewModel : NavigatableViewModel
{
    private readonly ITrackingServiceContext _trackingService;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly IResumePlaybackService _playbackStateStorage;
    private readonly IAnimeServiceContext _animeService;
    private readonly IStreamPageMapper _streamPageMapper;
    private readonly IVideoStreamResolverFactory _videoStreamResolverFactory;
    private readonly List<IMediaEventListener> _mediaEventListeners;
    private readonly ProviderOptions _providerOptions;
    private readonly bool _isCrunchyroll;

    private int? _episodeRequest;
    private IVideoStreamModelResolver _videoStreamResolver;
    private IAnimeModel _anime;

    public WatchViewModel(IProviderFactory providerFactory,
                          ITrackingServiceContext trackingService,
                          IViewService viewService,
                          ISettings settings,
                          ITimestampsService timestampsService,
                          IResumePlaybackService playbackStateStorage,
                          IAnimeServiceContext animeService,
                          IMediaPlayerFactory mediaPlayerFactory,
                          IStreamPageMapper streamPageMapper,
                          IVideoStreamResolverFactory videoStreamResolverFactory,
                          IEnumerable<IMediaEventListener> mediaEventListeners)
    {
        _trackingService = trackingService;
        _viewService = viewService;
        _settings = settings;
        _playbackStateStorage = playbackStateStorage;
        _animeService = animeService;
        _streamPageMapper = streamPageMapper;
        _videoStreamResolverFactory = videoStreamResolverFactory;
        _mediaEventListeners = mediaEventListeners.ToList();
        _providerOptions = providerFactory.GetOptions(_settings.DefaultProviderType);
        _isCrunchyroll = _settings.DefaultProviderType == "consumet" && _providerOptions.GetString("Provider", "zoro") == "crunchyroll";

        MediaPlayer = mediaPlayerFactory.Create(settings.MediaPlayerType);
        Provider = providerFactory.GetProvider(settings.DefaultProviderType);
        SelectedAudioStream = GetDefaultAudioStream();
        MediaPlayerType = _settings.MediaPlayerType;

        NextEpisode = ReactiveCommand.Create(() =>
        {
            MediaPlayer.Pause();
            EpisodeModels.SelectNext();
        }, HasNextEpisode(), RxApp.MainThreadScheduler);
        PrevEpisode = ReactiveCommand.Create(() =>
        {
            MediaPlayer.Pause();
            EpisodeModels.SelectPrevious();
        }, HasPrevEpisode(), RxApp.MainThreadScheduler);
        ChangeQuality = ReactiveCommand.Create<string>(quality =>
        {
            SelectedQuality = null;
            SelectedQuality = quality;
        }, outputScheduler: RxApp.MainThreadScheduler);
        SubmitTimeStamp = ReactiveCommand.Create(OnSubmitTimeStamps);

        this.ObservableForProperty(x => x.Anime, x => x)
            .WhereNotNull()
            .SelectMany(model => Find(model.Id, model.Title))
            .Where(x => x is not (null, null))
            .Log(this, "Selected Anime", x => $"{x.Sub.Title}")
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async x =>
            {
                var hasSubDub = x is { Dub: { }, Sub: { } };
                var searResult = _settings.PreferSubs ? x.Dub ?? x.Sub : x.Sub;

                if (hasSubDub)
                {
                    await CreateAnimDLResolver(x.Sub.Url, x.Dub.Url);
                    SubStreams = new List<string>() { "Sub", "Dub" };
                    SelectedAudioStream = _settings.PreferSubs ? SubStreams.First() : SubStreams.Last();
                }
                else
                {
                    await CreateAnimDLResolver(searResult.Url);
                }
            }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.EpisodeModels)
            .WhereNotNull()
            .Log(this, "Episodes :", x => string.Join(",", x.Select(e => e.EpisodeNumber)))
            .Where(_ => !UseTorrents)
            .Select(_ => GetQueuedEpisode())
            .Where(RequestedEpisodeIsValid)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => EpisodeModels.SelectEpisode(x), RxApp.DefaultExceptionHandler.OnError); // .Subcribe(EpisodeModels.SelecteEpisode) throws exception

        this.WhenAnyValue(x => x.EpisodeModels)
            .WhereNotNull()
            .SelectMany(x => x.WhenAnyValue(x => x.Current))
            .WhereNotNull()
            .Where(ep => ep.EpisodeNumber > 0)
            .Do(_ => MediaPlayer.Pause())
            .Log(this, "Selected Episode :", ep => ep.EpisodeNumber.ToString())
            .Do(epModel =>
            {
                if (EpisodeModels.Count > 1)
                {
                    MediaPlayer.TransportControls.IsNextTrackButtonVisible = epModel != EpisodeModels.Last();
                    MediaPlayer.TransportControls.IsPreviousTrackButtonVisible = epModel != EpisodeModels.First();
                }

                SetEpisode(epModel.EpisodeNumber);
            })
            .SelectMany(epModel => _videoStreamResolver.ResolveEpisode(epModel.EpisodeNumber, SelectedAudioStream))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(stream => Streams = stream, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.SelectedAudioStream)
            .Where(_ => EpisodeModels?.Current is not null && _videoStreamResolver is not null)
            .SelectMany(type => _videoStreamResolver?.ResolveAllEpisodes(type))
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(epModels =>
            {
                _episodeRequest = EpisodeModels.Current.EpisodeNumber;
                EpisodeModels = epModels;
            }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.Streams)
            .WhereNotNull()
            .Do(stream =>
            {
                if (settings.DefaultProviderType != "gogo")
                {
                    SubStreams = stream.StreamTypes;
                }
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
                try
                {
                    SetVideoStreamModel(stream);
                    await MediaPlayer.SetMedia(stream);
                    MediaPlayer.Play(GetPlayerTime());
                }
                catch (Exception ex)
                {
                    this.Log().Error(ex);
                }
            });

        MediaPlayer
            .DurationChanged
            .Where(_ => Anime is not null)
            .Throttle(TimeSpan.FromSeconds(1))
            .SelectMany(duration => timestampsService.GetTimeStamps(Anime.Id, EpisodeModels.Current.EpisodeNumber, duration.TotalSeconds))
            .Where(timeStamp => timeStamp.Success)
            .Subscribe(timeStamps =>
            {
                foreach (var item in mediaEventListeners)
                {
                    item.SetTimeStamps(timeStamps);
                }
            }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Subscribe(SetAnime);

        this.WhenAnyValue(x => x.SubStreams)
            .WhereNotNull()
            .Select(x => x.Count() > 1)
            .ToPropertyEx(this, x => x.HasMultipleSubStreams);

        NativeMethods.PreventSleep();
    }

    [Reactive] public bool UseTorrents { get; set; }
    [Reactive] public string SelectedAudioStream { get; set; }
    [Reactive] public EpisodeModelCollection EpisodeModels { get; set; }
    [Reactive] public IEnumerable<string> Qualities { get; set; }
    [Reactive] public IEnumerable<string> SubStreams { get; set; }
    [Reactive] public string SelectedQuality { get; set; }
    [Reactive] public VideoStreamsForEpisodeModel Streams { get; set; }
    [Reactive] public VideoStreamModel SelectedStream { get; set; }
    [Reactive] public string DownloadSpeed { get; set; }
    [Reactive] public string TotalDownloaded { get; set; }
    [Reactive] public bool ShowDownloadStats { get; set; }
    [ObservableAsProperty] public bool HasMultipleSubStreams { get; }

    public MediaPlayerType MediaPlayerType { get; }
    public IProvider Provider { get; }
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
    public ICommand ChangeQuality { get; }
    public ICommand SubmitTimeStamp { get; }

    public void SubscribeTransportControlEvents()
    {
        MediaPlayer
            .TransportControls
            .OnNextTrack
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => EpisodeModels.SelectNext());

        MediaPlayer
            .TransportControls
            .OnPrevTrack
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => EpisodeModels.SelectPrevious());

        MediaPlayer
            .TransportControls
            .OnStaticSkip
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => MediaPlayer.Seek(TimeSpan.FromSeconds(_settings.OpeningSkipDurationInSeconds), SeekDirection.Forward));

        MediaPlayer
            .TransportControls
            .OnQualityChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(quality =>
            {
                SelectedQuality = null;
                SelectedQuality = quality;
            });

        MediaPlayer
            .TransportControls
            .OnSubmitTimeStamp
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => OnSubmitTimeStamps());
    }

    public override async Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters.ContainsKey("Anime"))
        {
            Anime = parameters["Anime"] as IAnimeModel;
        }
        else if (parameters.ContainsKey("EpisodeInfo"))
        {
            var epInfo = parameters["EpisodeInfo"] as AiredEpisode;
            _episodeRequest = epInfo.Episode;
            _ = CreateAnimDLResolver(epInfo.Url);
            _ = TrySetAnime(epInfo.Url, epInfo.Title);
        }
        else if (parameters.ContainsKey("Id"))
        {
            var id = (long)parameters["Id"];
            Anime = await _animeService.GetInformation(id);
        }
        else if (parameters.ContainsKey("SearchResult"))
        {
            var searchResult = (SearchResult)parameters["SearchResult"];
            _ = CreateAnimDLResolver(searchResult.Url);
            _ = TrySetAnime(searchResult.Url, searchResult.Title);
        }
        else if (parameters.ContainsKey("Torrent"))
        {
            UseTorrents = true;
            Torrent = (TorrentModel)parameters["Torrent"];
            var useDebrid = (bool)parameters["UseDebrid"];
            _ = InitializeFromTorrent(Torrent, useDebrid);
        }
    }

    public override async Task OnNavigatedFrom()
    {
        MediaPlayer.Dispose();
        NativeMethods.AllowSleep();
        if (_videoStreamResolver is IAsyncDisposable ad)
        {
            await ad.DisposeAsync();
        }
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
            await GetMediaEventListener<IAniskip>()?.SubmitTimeStamp();
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

        var id = await _streamPageMapper.GetIdFromUrl(url, _settings.DefaultProviderType) ?? await TryGetId(title);

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
        SetAnime(_anime);
    }

    private async Task<long?> TryGetId(string title)
    {
        if (_settings.DefaultProviderType == "kamy") // kamy combines seasons to single series, had to update tracking 
        {
            return 0;
        }

        return await _viewService.TryGetId(title);
    }

    private string GetDefaultAudioStream()
    {
        if (_settings.DefaultProviderType == "gogo")
        {
            return "Sub";
        }

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
            return 0;
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

    private void SetVideoStreamModel(VideoStreamModel videoStreamModel)
    {
        foreach (var item in _mediaEventListeners)
        {
            item.SetVideoStreamModel(videoStreamModel);
        }
    }

    public void SetMediaPlayer(IMediaPlayer mediaPlayer)
    {
        foreach (var item in _mediaEventListeners)
        {
            item.SetMediaPlayer(mediaPlayer);
        }
    }

    private TEventListener GetMediaEventListener<TEventListener>()
    {
        return (TEventListener)_mediaEventListeners.FirstOrDefault(x => x is TEventListener);
    }

    private async Task InitializeFromTorrent(TorrentModel torrent, bool useDebrid)
    {
        var parsedResult = AnitomySharp.AnitomySharp.Parse(torrent.Name);
        var titleObj = parsedResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementAnimeTitle);
        if (titleObj is not null)
        {
            await TrySetAnime(titleObj.Value);
        }

        _videoStreamResolver = useDebrid
            ? await _videoStreamResolverFactory.CreateDebridStreamResolver(torrent.MagnetLink)
            : _videoStreamResolverFactory.CreateMonoTorrentStreamResolver(parsedResult, torrent.Link);

        if (_videoStreamResolver is INotifyDownloadStatus inds)
        {
            ShowDownloadStats = true;
            inds.Status
                .Subscribe(x =>
                {
                    DownloadSpeed = $"{x.DownloadSpeed.Bytes().Humanize()}/s";
                    TotalDownloaded = x.DataBytesDownloaded.Bytes().Humanize();
                });
        }

        EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes("");

        if (EpisodeModels.Count == 1)
        {
            EpisodeModels.Current = EpisodeModels.FirstOrDefault();
        }
    }

    private async Task CreateAnimDLResolver(string url)
    {
        _videoStreamResolver = _videoStreamResolverFactory.CreateAnimDLResolver(url);
        EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes(SelectedAudioStream);
    }

    private async Task CreateAnimDLResolver(string sub, string dub)
    {
        _videoStreamResolver = _videoStreamResolverFactory.CreateGogoAnimDLResolver(sub, dub);
        EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes(SelectedAudioStream);
    }
}