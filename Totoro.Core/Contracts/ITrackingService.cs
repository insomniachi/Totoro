namespace Totoro.Core.Contracts;

public interface ITrackingService
{
    ListServiceType Type { get; }
    Task<Tracking> Update(long id, Tracking tracking);
    Task<bool> Delete(long id);
    IAsyncEnumerable<AnimeModel> GetAnime();
    IAsyncEnumerable<AnimeModel> GetCurrentlyAiringTrackedAnime();
    Task<User> GetUser();
    void SetAccessToken(string accessToken);
    bool IsAuthenticated { get; }
}

public interface ITrackingServiceContext
{
    IObservable<ListServiceType> Authenticated { get; }
    Task<Tracking> Update(long id, Tracking tracking);
    Task<bool> Delete(long id);
    IAsyncEnumerable<AnimeModel> GetAnime();
    IAsyncEnumerable<AnimeModel> GetCurrentlyAiringTrackedAnime();
    void SetAccessToken(string token, ListServiceType type);
    bool IsAuthenticated { get; }
    bool IsTrackerAuthenticated(ListServiceType type);
    Task<User> GetUser();
}
