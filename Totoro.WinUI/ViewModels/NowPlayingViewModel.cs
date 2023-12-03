using Totoro.Core.ViewModels;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.MediaDetection;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.ViewModels;

public class NowPlayingViewModel : NavigatableViewModel
{
    private readonly IAnimeServiceContext _animeServiceContext;
    private readonly ITimestampsService _timestampsService;
    private readonly IAnimeDetectionService _animeDetectionService;
    private readonly NativeMediaPlayerTrackingUpdater _trackingUpdater;
    private readonly NativeMediaPlayerDiscordRichPresenseUpdater _discordRichPresenseUpdater;

    public NowPlayingViewModel(IAnimeServiceContext animeServiceContext,
                               ITimestampsService timestampsService,
                               IToastService toastService,
                               IAnimeDetectionService animeDetectionService,
                               ProcessWatcher watcher,
                               NativeMediaPlayerTrackingUpdater trackingUpdater,
                               NativeMediaPlayerDiscordRichPresenseUpdater discordRichPresenseUpdater)
    {
        _animeServiceContext = animeServiceContext;
        _timestampsService = timestampsService;
        _animeDetectionService = animeDetectionService;
        _trackingUpdater = trackingUpdater;
        _discordRichPresenseUpdater = discordRichPresenseUpdater;

        watcher
            .MediaPlayerClosed
            .Where(_ => MediaPlayer is not null)
            .Where(id => MediaPlayer.Process.Id == id)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                if (Anime is not null && IsVisible && (Anime.Tracking?.WatchedEpisodes ?? 0) < EpisodeInt)
                {
                    toastService.CheckEpisodeComplete(Anime, EpisodeInt);
                }

                MediaPlayer.Dispose();
                IsVisible = false;
                Anime = null;
                Episode = string.Empty;
                EpisodeInt = 0;
            });
        
        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Subscribe(anime =>
            {
                trackingUpdater.SetAnime(anime);
                discordRichPresenseUpdater.SetAnime(anime);
                discordRichPresenseUpdater.Initialize();
                toastService.Playing(Anime, Episode);
            });

        this.WhenAnyValue(x => x.Episode)
            .Subscribe(ep =>
            {
                if (!int.TryParse(ep, out int epInt))
                {
                    return;
                }

                EpisodeInt = epInt;
                trackingUpdater.SetCurrentEpisode(epInt);
                discordRichPresenseUpdater.SetCurrentEpisode(epInt);
            });

        this.WhenAnyValue(x => x.Anime, x => x.Duration)
            .Where(x => x.Item2 > TimeSpan.Zero && EpisodeInt > 0 && x.Item1 is not null)
            .SelectMany(x => GetTimeStamps(x.Item1, x.Item2))
            .Subscribe(trackingUpdater.SetTimeStamps, RxApp.DefaultExceptionHandler.OnError);
    }

    [ObservableAsProperty] public TimeSpan Duration { get; }
    [ObservableAsProperty] public TimeSpan Position { get; }
    [Reactive] public AnimeModel Anime { get; set; }
    [Reactive] public string Episode { get; set; }
    [Reactive] public bool IsVisible { get; set; }
    [Reactive] public ObservableCollection<KeyValuePair<string,string>> Info { get; set; }
    [Reactive] public bool IsPositionVisible { get; set; }
    [Reactive] public EpisodeModelCollection EpisodeModels { get; set; }
    public int EpisodeInt { get; set; }
    public INativeMediaPlayer MediaPlayer { get; set; }
    public string ProviderType { get; set; }
    public AnimeProvider Provider { get; set; }


    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if(parameters.ContainsKey("Player"))
        {
            IsVisible = true;
            MediaPlayer = (INativeMediaPlayer)parameters["Player"];
            InitializeFromPlayer(MediaPlayer);
        }

        return Task.CompletedTask;
    }

    public async Task SetAnime(long animeId)
    {
        Anime = await _animeServiceContext.GetInformation(animeId);

        Info = new ObservableCollection<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Title :", Anime.Title),
            new KeyValuePair<string, string>("Episode :", $"({Episode}/{(Anime.TotalEpisodes is null ? "?" : Anime.TotalEpisodes.ToString())})"),
            new KeyValuePair<string, string>("Airing Status :", Converters.EnumToDescription(Anime.AiringStatus)),
            new KeyValuePair<string, string>("Season :", $"{Anime.Season.SeasonName} {Anime.Season.Year}"),
            new KeyValuePair<string, string>("Rating :", Anime.MeanScore.ToString()),
            new KeyValuePair<string, string>("Genres :", string.Join(", ", Anime.Genres)),
        };
    }

    public async void InitializeFromPlayer(INativeMediaPlayer player)
    {
        IsPositionVisible = player is IHavePosition;

        if(player is IHavePosition ihp)
        {
            _trackingUpdater.SetMediaPlayer(ihp);
            _discordRichPresenseUpdater.SetMediaPlayer(ihp);

            ihp.DurationChanged
               .ObserveOn(RxApp.MainThreadScheduler)
               .ToPropertyEx(this, x => x.Duration, initialValue: TimeSpan.Zero);

            ihp.PositionChanged
               .ObserveOn(RxApp.MainThreadScheduler)
               .ToPropertyEx(this, x => x.Position, initialValue: TimeSpan.Zero);
        }

        var fileName = player.GetTitle();
        var parsedResults = AnitomySharp.AnitomySharp.Parse(fileName);
        var title = parsedResults.FirstOrDefault(x => x.Category == AnitomySharp.Element.ElementCategory.ElementAnimeTitle)?.Value;

        if(string.IsNullOrEmpty(title))
        {
            return;
        }

        Episode = parsedResults.FirstOrDefault(x => x.Category == AnitomySharp.Element.ElementCategory.ElementEpisodeNumber)?.Value;

        Info = new ObservableCollection<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Title :", title),
            new KeyValuePair<string, string>("Episode :", Episode),
        };

        var id = await _animeDetectionService.DetectFromFileName(title, true);

        if(id is not { } animeId)
        {
            return;
        }

        await SetAnime(animeId);
    }

    private Task<AniSkipResult> GetTimeStamps(AnimeModel anime, TimeSpan duration)
    {
        return anime.MalId is { } malId
            ? _timestampsService.GetTimeStampsWithMalId(malId, EpisodeModels.Current.EpisodeNumber, duration.TotalSeconds)
            : _timestampsService.GetTimeStampsWithMalId(anime.Id, EpisodeModels.Current.EpisodeNumber, duration.TotalSeconds);
    }
}
