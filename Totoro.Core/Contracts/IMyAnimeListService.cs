namespace Totoro.Core.Contracts;

public interface IMyAnimeListService
{
    Task<IEnumerable<EpisodeModel>> GetEpisodes(long id);
    IAsyncEnumerable<int> GetFillers(long id);
    IAsyncEnumerable<AnimeModel> GetAiringAnime();
    IAsyncEnumerable<AnimeModel> GetUpcomingAnime();
    IAsyncEnumerable<AnimeModel> GetPopularAnime();
    IAsyncEnumerable<AnimeModel> GetRecommendedAnime();
}
