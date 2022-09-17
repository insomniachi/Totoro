namespace Totoro.Core.Contracts;

public interface IRecentEpisodesProvider
{
    IObservable<IEnumerable<AiredEpisode>> GetRecentlyAiredEpisodes();
}


