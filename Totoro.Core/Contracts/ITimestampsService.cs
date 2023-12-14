namespace Totoro.Core.Contracts
{
    public interface ITimestampsService
    {
        Task<TimestampResult> GetTimeStampsWithMalId(long id, int ep, double duration);
        Task<TimestampResult> GetTimeStamps(long id, int ep, double duration);
        Task SubmitTimeStamp(long id, int ep, string skipType, Interval interval, double episodeLength);
        Task SubmitTimeStampWithMalId(long id, int ep, string skipType, Interval interval, double episodeLength);
        Task Vote(Guid skipId, bool isThumpsUp);
    }
}