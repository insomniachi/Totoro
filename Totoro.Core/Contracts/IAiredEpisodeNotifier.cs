using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Contracts;

public interface IAiredEpisodeNotifier
{
    IObservable<IAiredAnimeEpisode> OnNewEpisode { get; }
    void Start();
}
