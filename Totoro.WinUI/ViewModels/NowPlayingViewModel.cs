using Totoro.Core.ViewModels;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.WinUI.ViewModels;

public class NowPlayingViewModel : NavigatableViewModel
{
    private readonly IViewService _viewService;
    private readonly IAnimeServiceContext _animeServiceContext;

    public NowPlayingViewModel(IViewService viewService,
                               IAnimeServiceContext animeServiceContext)
    {
        _viewService = viewService;
        _animeServiceContext = animeServiceContext;
    }

    [ObservableAsProperty] public TimeSpan Duration { get; }
    [ObservableAsProperty] public TimeSpan Position { get; }
    [Reactive] public AnimeModel Anime { get; set; }
    [Reactive] public string Episode { get; set; }

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
    }
}
