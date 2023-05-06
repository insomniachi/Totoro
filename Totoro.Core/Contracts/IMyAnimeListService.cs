namespace Totoro.Core.Contracts;

public interface IMyAnimeListService
{
    Task<IEnumerable<EpisodeModel>> GetEpisodes(long id);
}
