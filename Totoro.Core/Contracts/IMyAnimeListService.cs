namespace Totoro.Core.Contracts;

public interface IMyAnimeListService
{
    Task<IEnumerable<EpisodeModel>> GetEpisodes(long id);
    IAsyncEnumerable<int> GetFillers(long id);
    IObservable<IEnumerable<AnimeModel>> GetAiringAnime();
    IObservable<IEnumerable<AnimeModel>> GetUpcomingAnime();
    IObservable<IEnumerable<AnimeModel>> GetPopularAnime();
    IObservable<IEnumerable<AnimeModel>> GetRecommendedAnime();
}
