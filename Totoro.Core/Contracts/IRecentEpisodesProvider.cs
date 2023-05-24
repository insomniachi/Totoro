using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Contracts;

public interface IRecentEpisodesProvider
{
    IObservable<IAiredAnimeEpisode> GetRecentlyAiredEpisodes();
    IObservable<long> GetMalId(IAiredAnimeEpisode ep);
}


