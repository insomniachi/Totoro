namespace Totoro.Core.Contracts;

public interface IAnilistService : IAnimeService
{
    Task<(int? Episode, DateTime? Time)> GetNextAiringEpisode(long id);
    Task<string> GetBannerImage(long id);
}