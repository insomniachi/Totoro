namespace Totoro.Core.Contracts;

public interface ITrackingService
{
    ListServiceType Type { get; }
    IObservable<Tracking> Update(long id, Tracking tracking);
    IObservable<bool> Delete(long id);
    IObservable<IEnumerable<AnimeModel>> GetAnime();
    IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime();
    Task<User> GetUser();
    void SetAccessToken(string accessToken);
    bool IsAuthenticated { get; }
}

public interface ITrackingServiceContext
{
    IObservable<ListServiceType> Authenticated { get; }
    IObservable<Tracking> Update(long id, Tracking tracking);
    IObservable<bool> Delete(long id);
    IObservable<IEnumerable<AnimeModel>> GetAnime();
    IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime();
    void SetAccessToken(string token, ListServiceType type);
    bool IsAuthenticated { get; }
    bool IsTrackerAuthenticated(ListServiceType type);
    Task<User> GetUser();
}
