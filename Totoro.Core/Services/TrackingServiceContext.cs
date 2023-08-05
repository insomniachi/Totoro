using System.Reactive.Subjects;
using Totoro.Core.Models;

namespace Totoro.Core.Services;

public class TrackingServiceContext : ITrackingServiceContext
{
    private readonly Dictionary<ListServiceType, ITrackingService> _trackers;
    private readonly ISettings _settings;
    private readonly IConnectivityService _connectivityService;
    private readonly Subject<ListServiceType> _authenticatedSubject = new();

    public TrackingServiceContext(ISettings settings,
                                  IEnumerable<ITrackingService> trackers,
                                  IConnectivityService connectivityService)
    {
        _settings = settings;
        _connectivityService = connectivityService;
        _trackers = trackers.Any()
            ? trackers.ToDictionary(x => x.Type, x => x)
            : new();
    }

    public bool IsAuthenticated => _trackers[_settings.DefaultListService].IsAuthenticated;
    public bool IsTrackerAuthenticated(ListServiceType type) => _trackers[type].IsAuthenticated;
    public IObservable<ListServiceType> Authenticated => _authenticatedSubject;
    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _trackers[_settings.DefaultListService].GetAnime();
    }

    public IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _trackers[_settings.DefaultListService].GetCurrentlyAiringTrackedAnime();
    }

    public void SetAccessToken(string token, ListServiceType type)
    {
        _trackers[type].SetAccessToken(token);
        _authenticatedSubject.OnNext(type);
    }

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(tracking);
        }

        return _trackers[_settings.DefaultListService].Update(id, tracking);
    }

    public IObservable<bool> Delete(long id)
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(false);
        }

        return _trackers[_settings.DefaultListService].Delete(id);
    }
}
