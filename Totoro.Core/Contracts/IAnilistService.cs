namespace Totoro.Core.Contracts
{
    public interface IAnilistService : IAnimeService
    {
        Task<int?> GetNextAiringEpisode(long id);
        Task<DateTime?> GetNextAiringEpisodeTime(long id);
        Task<string> GetBannerImage(long id); 
    }
}