namespace Totoro.Core.Contracts;

public interface ITrackingService
{
    ListServiceType Type { get; }
    IObservable<Tracking> Update(long id, Tracking tracking);
    IObservable<IEnumerable<AnimeModel>> GetAnime();
    IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime();
    bool IsAuthenticated { get; }
}

public interface ITrackingServiceContext
{
    IObservable<Tracking> Update(long id, Tracking tracking);
    IObservable<IEnumerable<AnimeModel>> GetAnime();
    IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime();
    bool IsAuthenticated { get; }
}
