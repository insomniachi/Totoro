namespace AnimDL.UI.Core.Contracts;

public interface IRecentEpisodesProvider
{
    IObservable<IEnumerable<AiredEpisode>> GetRecentlyAiredEpisodes();
}


