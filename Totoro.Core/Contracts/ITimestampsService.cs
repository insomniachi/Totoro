namespace Totoro.Core.Contracts
{
    public interface ITimestampsService
    {
        Task<AniSkipResult> GetTimeStampsWithMalId(long id, int ep, double duration);
        Task<AniSkipResult> GetTimeStamps(long id, int ep, double duration);
        Task SubmitTimeStamp(long id, int ep, string skipType, Interval interval, double episodeLength);
        Task SubmitTimeStampWithMalId(long id, int ep, string skipType, Interval interval, double episodeLength);
        Task Vote(string skipId, bool isThumpsUp);
    }
}