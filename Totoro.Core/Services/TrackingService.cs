namespace Totoro.Core.Services;

public class TrackingServiceContext : ITrackingServiceContext
{
    private readonly Dictionary<ListServiceType, ITrackingService> _trackers;
    private readonly ISettings _settings;

    public TrackingServiceContext(ISettings settings,
                                  IEnumerable<ITrackingService> trackers)
    {
        _settings = settings;

        _trackers = trackers.Any()
            ? trackers.ToDictionary(x => x.Type, x => x)
            : new();
    }

    public bool IsAuthenticated
    {
        get
        {
            if (_settings.DefaultListService is not ListServiceType type)
            {
                return false;
            }

            return _trackers[type].IsAuthenticated;
        }
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        if (_settings.DefaultListService is not ListServiceType type)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _trackers[type].GetAnime();
    }

    public IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        if (_settings.DefaultListService is not ListServiceType type)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _trackers[type].GetCurrentlyAiringTrackedAnime();
    }

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        if (_settings.DefaultListService is not ListServiceType type)
        {
            return Observable.Return(tracking);
        }

        return _trackers[type].Update(id, tracking);
    }
}
