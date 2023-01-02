namespace Totoro.Core.Contracts
{
    public interface ITimestampsService
    {
        Task<AniSkipResult> GetTimeStamps(long id, int ep, double duration);
        Task SubmitTimeStamp(long id, int ep, string skipType, Interval interval, double episodeLength);
    }
}