using System.Reactive.Subjects;

namespace Totoro.Core.Services;

public class TrackingServiceContext : ITrackingServiceContext
{
    private readonly Dictionary<ListServiceType, ITrackingService> _trackers;
    private readonly ISettings _settings;
    private readonly Subject<ListServiceType> _authenticatedSubject = new();

    public TrackingServiceContext(ISettings settings,
                                  IEnumerable<ITrackingService> trackers)
    {
        _settings = settings;

        _trackers = trackers.Any()
            ? trackers.ToDictionary(x => x.Type, x => x)
            : new();
    }

    public bool IsAuthenticated => _trackers[_settings.DefaultListService].IsAuthenticated;
    public bool IsTrackerAuthenticated(ListServiceType type) => _trackers[type].IsAuthenticated;
    public IObservable<ListServiceType> Authenticated => _authenticatedSubject;
    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        return _trackers[_settings.DefaultListService].GetAnime();
    }

    public IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        return _trackers[_settings.DefaultListService].GetCurrentlyAiringTrackedAnime();
    }

    public void SetAccessToken(string token, ListServiceType type)
    {
        _trackers[type].SetAccessToken(token);
        _authenticatedSubject.OnNext(type);
    }

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        return _trackers[_settings.DefaultListService].Update(id, tracking);
    }
}
