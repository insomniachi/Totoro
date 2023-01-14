namespace Totoro.Core.Contracts
{
    public interface IAnimeScheduleService
    {
        Task<int?> GetNextAiringEpisode(long id);
        Task<DateTime?> GetNextAiringEpisodeTime(long id);
    }
}