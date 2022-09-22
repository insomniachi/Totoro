namespace Totoro.Core.Contracts;

public interface IRecentEpisodesProvider
{
    IObservable<IEnumerable<AiredEpisode>> GetRecentlyAiredEpisodes();
    IObservable<long> GetMalId(AiredEpisode ep);
}


