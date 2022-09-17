using Totoro.Core.Services;

namespace Totoro.Core.Contracts
{
    public interface ITimestampsService
    {
        Task<AnimeTimeStamps> GetTimeStamps(long malId);
    }
}