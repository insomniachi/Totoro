using Totoro.Core.Services.Anizip;

namespace Totoro.Core.ViewModels.About;

public class AnimeEpisodesViewModel : BaseAboutAnimeViewModel
{
    public AnimeEpisodesViewModel(INavigationService navigationService)
    {
        Watch = ReactiveCommand.Create<EpisodeInfo>(info =>
        {
            navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>
            {
                { WatchViewModelParamters.Anime, Anime },
                { WatchViewModelParamters.EpisodeNumber, info.EpisodeNumber }
            });
        });
    }

    [Reactive] public List<EpisodeInfo> Episodes { get; set; }
    public ICommand Watch { get; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        Episodes = (List<EpisodeInfo>)parameters.GetValueOrDefault(nameof(Episodes), new List<EpisodeInfo>());

        return base.OnNavigatedTo(parameters);
    }
}
