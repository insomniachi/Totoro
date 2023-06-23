using Totoro.Core.ViewModels;
using Totoro.Plugins.MediaDetection;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.WinUI.ViewModels;

public class NowPlayingViewModel : NavigatableViewModel
{
    private readonly IViewService _viewService;
    private readonly IAnimeServiceContext _animeServiceContext;
    private readonly NativeMediaPlayerTrackingUpdater _trackingUpdater;
    private readonly NativeMediaPlayerDiscordRichPresenseUpdater _discordRichPresenseUpdater;
    private INativeMediaPlayer _mediaPlayer;

    public NowPlayingViewModel(IViewService viewService,
                               IAnimeServiceContext animeServiceContext,
                               ITimestampsService timestampsService,
                               ProcessWatcher watcher,
                               NativeMediaPlayerTrackingUpdater trackingUpdater,
                               NativeMediaPlayerDiscordRichPresenseUpdater discordRichPresenseUpdater)
    {
        _viewService = viewService;
        _animeServiceContext = animeServiceContext;
        _trackingUpdater = trackingUpdater;
        _discordRichPresenseUpdater = discordRichPresenseUpdater;

        watcher
            .MediaPlayerClosed
            .Where(id => _mediaPlayer.ProcessId == id)
            .Subscribe(_ => _mediaPlayer.Dispose());
        
        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Subscribe(anime =>
            {
                trackingUpdater.SetAnime(anime);
                discordRichPresenseUpdater.SetAnime(anime);
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
            .SelectMany(x => timestampsService.GetTimeStamps(x.Item1.Id, EpisodeInt, x.Item2.TotalSeconds))
            .Subscribe(trackingUpdater.SetTimeStamps, RxApp.DefaultExceptionHandler.OnError);
    }

    [ObservableAsProperty] public TimeSpan Duration { get; }
    [ObservableAsProperty] public TimeSpan Position { get; }
    [Reactive] public AnimeModel Anime { get; set; }
    [Reactive] public string Episode { get; set; }
    public int EpisodeInt { get; set; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if(parameters.ContainsKey("Player"))
        {
            var player = (INativeMediaPlayer)parameters["Player"];
            InitializeFromPlayer(player);
        };

        return Task.CompletedTask;
    }

    public async void InitializeFromPlayer(INativeMediaPlayer player)
    {
        _mediaPlayer = player;
        _trackingUpdater.SetMediaPlayer(player);
        _discordRichPresenseUpdater.SetMediaPlayer(player);

        player
            .DurationChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.Duration, initialValue: TimeSpan.Zero).DisposeWith(Garbage);
        
        player
            .PositionChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .ToPropertyEx(this, x => x.Position, initialValue: TimeSpan.Zero).DisposeWith(Garbage);

        var fileName = player.GetTitle();
        var parsedResults = AnitomySharp.AnitomySharp.Parse(fileName);
        var title = parsedResults.FirstOrDefault(x => x.Category == AnitomySharp.Element.ElementCategory.ElementAnimeTitle)?.Value;

        if(string.IsNullOrEmpty(title))
        {
            return;
        }

        Episode = parsedResults.FirstOrDefault(x => x.Category == AnitomySharp.Element.ElementCategory.ElementEpisodeNumber)?.Value;

        var id = await _viewService.TryGetId(title);

        if(id is not { } animeId)
        {
            return;
        }

        Anime = await _animeServiceContext.GetInformation(animeId);

        _discordRichPresenseUpdater.Initialize();
    }
}
