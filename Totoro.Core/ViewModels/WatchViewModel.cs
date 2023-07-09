using System.Reactive.Concurrency;
using AnitomySharp;
using FuzzySharp;
using Humanizer;
using MonoTorrent.Client;
using Splat;
using Totoro.Core.Helpers;
using Totoro.Core.Services.MediaEvents;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Options;
using Totoro.Plugins.Torrents.Models;

namespace Totoro.Core.ViewModels;

public partial class WatchViewModel : NavigatableViewModel
{
    private readonly IPluginFactory<AnimeProvider> _providerFactory;
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private readonly ITimestampsService _timestampsService;
    private readonly IResumePlaybackService _playbackStateStorage;
    private readonly IAnimeServiceContext _animeService;
    private readonly IStreamPageMapper _streamPageMapper;
    private readonly IVideoStreamResolverFactory _videoStreamResolverFactory;
    private readonly IMyAnimeListService _myAnimeListService;
    private readonly List<IMediaEventListener> _mediaEventListeners;
    
    private PluginOptions _providerOptions;
    private bool _isCrunchyroll;
    private IEnumerable<EpisodeModel> _episodeMetadata;
    private int? _episodeRequest;
    private IVideoStreamModelResolver _videoStreamResolver;
    private IAnimeModel _anime;

    public WatchViewModel(IPluginFactory<AnimeProvider> providerFactory,
                          IViewService viewService,
                          ISettings settings,
                          ITimestampsService timestampsService,
                          IResumePlaybackService playbackStateStorage,
                          IAnimeServiceContext animeService,
                          IMediaPlayerFactory mediaPlayerFactory,
                          IStreamPageMapper streamPageMapper,
                          IVideoStreamResolverFactory videoStreamResolverFactory,
                          IEnumerable<IMediaEventListener> mediaEventListeners,
                          IMyAnimeListService myAnimeListService)
    {
        _providerFactory = providerFactory;
        _viewService = viewService;
        _settings = settings;
        _timestampsService = timestampsService;
        _playbackStateStorage = playbackStateStorage;
        _animeService = animeService;
        _streamPageMapper = streamPageMapper;
        _videoStreamResolverFactory = videoStreamResolverFactory;
        _myAnimeListService = myAnimeListService;
        _mediaEventListeners = mediaEventListeners.ToList();

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

        this.WhenAnyValue(x => x.MediaPlayerType)
            .WhereNotNull()
            .Select(x => mediaPlayerFactory.Create(x.Value))
            .ToPropertyEx(this, x => x.MediaPlayer, initialValue: null);


        this.WhenAnyValue(x => x.ProviderType)
            .WhereNotNull()
            .Subscribe(type =>
            {
                Provider = _providerFactory.CreatePlugin(type);
                RxApp.MainThreadScheduler.Schedule(() => MediaPlayer.TransportControls.IsAddCCButtonVisibile = UseTorrents || type == "zoro");
            });

        this.ObservableForProperty(x => x.Anime, x => x)
            .WhereNotNull()
            .Do(async model => _episodeMetadata ??= await myAnimeListService.GetEpisodes(model.Id))
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
                    SubStreams = new List<StreamType>() { StreamType.EnglishSubbed, StreamType.EnglishDubbed };
                    SelectedAudioStream = _settings.PreferSubs ? SubStreams.First() : SubStreams.Last();
                }
                else
                {
                    await CreateAnimDLResolver(searResult.Url);
                }
            }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.EpisodeModels)
            .WhereNotNull()
            .Log(this, "Episodes :", x => string.Join(",", x.Select(e => e.IsSpecial ? e.SpecialEpisodeNumber : e.EpisodeNumber.ToString())))
            .Select(_ => GetQueuedEpisode())
            .Where(RequestedEpisodeIsValid)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => EpisodeModels.SelectEpisode(x), RxApp.DefaultExceptionHandler.OnError); // .Subcribe(EpisodeModels.SelecteEpisode) throws exception

        this.WhenAnyValue(x => x.EpisodeModels)
            .Where(x => x is not null && Anime is not null && _episodeMetadata is not null)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                foreach (var item in _episodeMetadata)
                {
                    if(EpisodeModels.FirstOrDefault(x => x.EpisodeNumber == item.EpisodeNumber) is not { } ep)
                    {
                        continue;
                    }

                    ep.EpisodeTitle = item.EpisodeTitle;
                }
            });

        this.WhenAnyValue(x => x.EpisodeModels)
            .WhereNotNull()
            .SelectMany(x => x.WhenAnyValue(x => x.Current))
            .WhereNotNull()
            .Do(_ => MediaPlayer.Pause())
            .Log(this, "Selected Episode :", ep => ep.IsSpecial ? ep.SpecialEpisodeNumber : ep.EpisodeNumber.ToString())
            .Do(epModel =>
            {
                if (EpisodeModels.Count > 1)
                {
                    MediaPlayer.TransportControls.IsNextTrackButtonVisible = epModel != EpisodeModels.Last();
                    MediaPlayer.TransportControls.IsPreviousTrackButtonVisible = epModel != EpisodeModels.First();
                }

                SetEpisode(epModel.EpisodeNumber);
            })
            .SelectMany(epModel => epModel.IsSpecial
                    ? ((ISpecialVideoStreamModelResolver)_videoStreamResolver).ResolveSpecialEpisode(epModel.SpecialEpisodeNumber, SelectedAudioStream.Value)
                    : _videoStreamResolver.ResolveEpisode(epModel.EpisodeNumber, SelectedAudioStream.Value))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(stream => Streams = stream, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.SelectedAudioStream)
            .WhereNotNull()
            .Select(x => x.Value)
            .DistinctUntilChanged()
            .Where(_ => EpisodeModels?.Current is not null && _videoStreamResolver is not null)
            .SelectMany(type => _videoStreamResolver?.ResolveAllEpisodes(type))
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(epModels =>
            {
                _episodeRequest = EpisodeModels.Current?.EpisodeNumber;
                EpisodeModels = epModels;
            }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.Streams)
            .WhereNotNull()
            .Do(stream =>
            {
                if (ProviderType != "gogo-anime")
                {
                    var selectedAudioStream = SelectedAudioStream;
                    SubStreams = stream.StreamTypes;
                    SelectedAudioStream = selectedAudioStream;
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

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Subscribe(SetAnime);

        this.WhenAnyValue(x => x.SubStreams)
            .WhereNotNull()
            .Select(x => x.Count() > 1)
            .ToPropertyEx(this, x => x.HasMultipleSubStreams);

        GetMediaEventListener<ITrackingUpdater>().TrackingUpdated += (_, _) =>
        {
            if (_videoStreamResolver is not ICompletionAware completionAware)
            {
                return;
            }

            completionAware.OnCompleted();
        };

        NativeMethods.PreventSleep();
    }

    [Reactive] public bool UseTorrents { get; set; }
    [Reactive] public StreamType? SelectedAudioStream { get; set; }
    [Reactive] public EpisodeModelCollection EpisodeModels { get; set; }
    [Reactive] public IEnumerable<string> Qualities { get; set; }
    [Reactive] public IEnumerable<StreamType> SubStreams { get; set; }
    [Reactive] public string SelectedQuality { get; set; }
    [Reactive] public VideoStreamsForEpisodeModel Streams { get; set; }
    [Reactive] public VideoStreamModel SelectedStream { get; set; }
    [Reactive] public string DownloadSpeed { get; set; }
    [Reactive] public string TotalDownloaded { get; set; }
    [Reactive] public string DownloadProgress { get; set; }
    [Reactive] public bool ShowDownloadStats { get; set; }
    [Reactive] public MediaPlayerType? MediaPlayerType { get; private set; }
    [Reactive] public string ProviderType { get; set; }
    [ObservableAsProperty] public IMediaPlayer MediaPlayer { get; }
    [ObservableAsProperty] public bool HasMultipleSubStreams { get; }

    public AnimeProvider Provider { get; private set; }
    public IAnimeModel Anime
    {
        get => _anime;
        set => this.RaiseAndSetIfChanged(ref _anime, value);
    }

    public bool AutoFullScreen => _settings.EnterFullScreenWhenPlaying;
    public TorrentModel Torrent { get; private set; }

    public ICommand NextEpisode { get; }
    public ICommand PrevEpisode { get; }
    public ICommand ChangeQuality { get; }

    public void SubscribeTransportControlEvents()
    {
        MediaPlayer
            .DurationChanged
            .Where(_ => Anime is not null)
            .Throttle(TimeSpan.FromSeconds(1))
            .SelectMany(duration => _timestampsService.GetTimeStamps(Anime.Id, EpisodeModels.Current.EpisodeNumber, duration.TotalSeconds))
            .Where(timeStamp => timeStamp.Success)
            .Subscribe(timeStamps =>
            {
                foreach (var item in _mediaEventListeners)
                {
                    item.SetTimeStamps(timeStamps);
                }
            }, RxApp.DefaultExceptionHandler.OnError);

        MediaPlayer
            .PlaybackEnded
            .Where(_ => EpisodeModels.Current != EpisodeModels.Last())
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => EpisodeModels.SelectNext());

        MediaPlayer
            .TransportControls
            .OnNextTrack
            .ObserveOn(RxApp.MainThreadScheduler)
            .InvokeCommand(NextEpisode);

        MediaPlayer
            .TransportControls
            .OnPrevTrack
            .ObserveOn(RxApp.MainThreadScheduler)
            .InvokeCommand(PrevEpisode);

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
        UseTorrents = parameters.ContainsKey("TorrentModel") || parameters.ContainsKey("TorrentManager");
        MediaPlayerType = UseTorrents ? Models.MediaPlayerType.Vlc : _settings.MediaPlayerType;
        ProviderType = parameters.GetValueOrDefault("Provider", _settings.DefaultProviderType) as string;
        _providerOptions = _providerFactory.GetOptions(ProviderType);
        _isCrunchyroll = _settings.DefaultProviderType == "consumet" && _providerOptions.GetString("Provider", "zoro") == "crunchyroll";
        SelectedAudioStream = GetDefaultAudioStream();

        if (parameters.ContainsKey("Anime"))
        {
            Anime = parameters["Anime"] as IAnimeModel;
        }
        else if (parameters.ContainsKey("EpisodeInfo"))
        {
            var epInfo = parameters["EpisodeInfo"] as IAiredAnimeEpisode;
            _episodeRequest = epInfo.Episode;
            await TrySetAnime(epInfo.Url, epInfo.Title);
            await CreateAnimDLResolver(epInfo.Url);
        }
        else if (parameters.ContainsKey("Id"))
        {
            var id = (long)parameters["Id"];
            Anime = await _animeService.GetInformation(id);
        }
        else if (parameters.ContainsKey("SearchResult"))
        {
            var searchResult = (ICatalogItem)parameters["SearchResult"];
            if(searchResult is IHaveMalId  { MalId: >0 } ihmid && _settings.DefaultListService == ListServiceType.MyAnimeList)
            {
                SetAnime(ihmid.MalId);
            }
            else if(searchResult is IHaveAnilistId { AnilistId : >0 } ihaid && _settings.DefaultListService == ListServiceType.AniList)
            {
                SetAnime(ihaid.AnilistId);
            }
            else
            {
                await TrySetAnime(searchResult.Url, searchResult.Title);
            }

            await CreateAnimDLResolver(searchResult.Url);
        }
        else if (parameters.ContainsKey("TorrentModel"))
        {
            Torrent = (TorrentModel)parameters["TorrentModel"];
            var useDebrid = (bool)parameters["UseDebrid"];
            await InitializeFromTorrentModel(Torrent, useDebrid);
        }
        else if(parameters.ContainsKey("LocalFolder"))
        {
            var folder = (string)parameters["LocalFolder"];
            await TrySetAnime(Path.GetFileName(folder));
            await CreateLocalStreamResolver(folder);
        }
        else if(parameters.ContainsKey("TorrentManager"))
        {
            var torrentManager = (TorrentManager)parameters["TorrentManager"];
            await TrySetAnime(torrentManager.Torrent.Name);
            await CreateMonoTorrentStreamResolver(torrentManager.Torrent);
        }
    }

    public override async Task OnNavigatedFrom()
    {
        MediaPlayer?.Dispose();
        NativeMethods.AllowSleep();
        if (_videoStreamResolver is IAsyncDisposable ad)
        {
            await ad.DisposeAsync();
        }
        else if(_videoStreamResolver is IDisposable d)
        {
            d.Dispose();
        }
    }

    private async Task<(ICatalogItem Sub, ICatalogItem Dub)> Find(long id, string title)
    {
        if (Provider is null)
        {
            return (null, null);
        }

        return await _streamPageMapper.GetStreamPage(id, ProviderType) ?? await SearchProvider(title);
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

    private async Task<(ICatalogItem Sub, ICatalogItem Dub)> SearchProvider(string title)
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
        else if (results.Count == 2 && ProviderType is "gogo-anime") // gogo anime has separate listing for sub/dub
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
            return (await _viewService.ChoooseSearchResult(suggested, results, ProviderType), null);
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
        if(UseTorrents && EpisodeModels is { Count : 1 })
        {
            return _episodeRequest ?? EpisodeModels.First().EpisodeNumber;
        }

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

        var id = await _streamPageMapper.GetIdFromUrl(url, ProviderType) ?? await TryGetId(title);

        if (id is null)
        {
            this.Log().Warn($"Unable to find Id for {title}, watch session will not be tracked");
            return;
        }

        SetAnime(id.Value);
    }

    private async Task TrySetAnime(string title)
    {
        var id = await TryGetId(title);

        if (id is null)
        {
            this.Log().Warn($"Unable to find Id for {title}, watch session will not be tracked");
            return;
        }

        SetAnime(id.Value);
    }

    private void SetAnime(long id)
    {
        _animeService.GetInformation(id)
            .Subscribe(async anime =>
            {
                _anime = anime;
                _episodeMetadata = await _myAnimeListService.GetEpisodes(id);
                SetAnime(_anime);
            }, RxApp.DefaultExceptionHandler.OnError);
    }

    private async Task<long?> TryGetId(string title)
    {
        if (ProviderType == "kamy") // kamy combines seasons to single series, had to update tracking 
        {
            return 0;
        }

        return await _viewService.TryGetId(title);
    }

    private StreamType GetDefaultAudioStream()
    {
        if (ProviderType == "gogo-anime")
        {
            return StreamType.EnglishSubbed;
        }

        var key = "StreamType";
        if (ProviderType == "consumet" && _providerOptions.GetString("Provider", "zoro") == "crunchyroll")
        {
            key = "CrunchyrollStreamType";
        }

        return _providerOptions.GetEnum(key, StreamType.EnglishSubbed);
    }

    private double GetPlayerTime()
    {
        if (Anime is null || EpisodeModels.Current is null)
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

    private async Task InitializeFromTorrentModel(TorrentModel torrent, bool useDebrid)
    {
        var parsedResult = AnitomySharp.AnitomySharp.Parse(torrent.Name);
        var titleObj = parsedResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementAnimeTitle);
        if (titleObj is not null)
        {
            await TrySetAnime(titleObj.Value);
        }

        _videoStreamResolver = useDebrid
            ? await _videoStreamResolverFactory.CreateDebridStreamResolver(torrent.Magnet)
            : _videoStreamResolverFactory.CreateMonoTorrentStreamResolver(parsedResult, torrent.Link);

        ObserveDownload();

        EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes(StreamType.EnglishSubbed);
    }

    private async Task CreateAnimDLResolver(string url)
    {
        _videoStreamResolver = _videoStreamResolverFactory.CreateAnimDLResolver(ProviderType, url);
        EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes(SelectedAudioStream.Value);
    }

    private async Task CreateAnimDLResolver(string sub, string dub)
    {
        _videoStreamResolver = _videoStreamResolverFactory.CreateGogoAnimDLResolver(ProviderType, sub, dub);
        EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes(SelectedAudioStream.Value);
    }

    private async Task CreateLocalStreamResolver(string directory)
    {
        _videoStreamResolver = _videoStreamResolverFactory.CreateLocalStreamResolver(directory);
        EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes(SelectedAudioStream.Value);
    }

    private async Task CreateMonoTorrentStreamResolver(MonoTorrent.Torrent torrent)
    {
        _videoStreamResolver = _videoStreamResolverFactory.CreateMonoTorrentStreamResolver(torrent);
        EpisodeModels = await _videoStreamResolver.ResolveAllEpisodes(SelectedAudioStream.Value);
        ObserveDownload();
    }

    private void ObserveDownload()
    {
        if (_videoStreamResolver is not INotifyDownloadStatus inds)
        {
            return;
        }

        ShowDownloadStats = true;
        inds.Status
            .Subscribe(x =>
            {
                DownloadSpeed = $"{x.Item2.DownloadSpeed.Bytes().Humanize()}/s";
                TotalDownloaded = x.Item2.DataBytesDownloaded.Bytes().Humanize();
                DownloadProgress = x.Item1.ToString("N2");
            });
    }
}