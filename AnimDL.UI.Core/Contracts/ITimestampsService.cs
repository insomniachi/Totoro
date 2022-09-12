using AnimDL.UI.Core.Services;

namespace AnimDL.UI.Core.Contracts
{
    public interface ITimestampsService
    {
        Task<AnimeTimeStamps> GetTimeStamps(long malId);
    }
}