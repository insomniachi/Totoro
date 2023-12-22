using System.Reactive.Subjects;
using Microsoft.Extensions.DependencyInjection;

namespace Totoro.Core.Services;

public class TrackingServiceContext : ITrackingServiceContext
{
    private readonly Dictionary<ListServiceType, Lazy<ITrackingService>> _trackers;
    private readonly ISettings _settings;
    private readonly IConnectivityService _connectivityService;
    private readonly Subject<ListServiceType> _authenticatedSubject = new();

    public TrackingServiceContext(ISettings settings,
                                  [FromKeyedServices(ListServiceType.MyAnimeList)] Lazy<ITrackingService> myAnimeListService,
                                  [FromKeyedServices(ListServiceType.AniList)] Lazy<ITrackingService> anilistService,
                                  [FromKeyedServices(ListServiceType.Simkl)] Lazy<ITrackingService> simklService,
                                  IConnectivityService connectivityService)
    {
        _settings = settings;
        _connectivityService = connectivityService;
        _trackers = new()
        {
            { ListServiceType.MyAnimeList, myAnimeListService },
            { ListServiceType.AniList, anilistService },
            { ListServiceType.Simkl, simklService }
        };
    }

    public bool IsAuthenticated => _trackers[_settings.DefaultListService].Value.IsAuthenticated;
    public bool IsTrackerAuthenticated(ListServiceType type) => _trackers[type].Value.IsAuthenticated;
    public IObservable<ListServiceType> Authenticated => _authenticatedSubject;
    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _trackers[_settings.DefaultListService].Value.GetAnime();
    }

    public IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _trackers[_settings.DefaultListService].Value.GetCurrentlyAiringTrackedAnime();
    }

    public void SetAccessToken(string token, ListServiceType type)
    {
        _trackers[type].Value.SetAccessToken(token);
        _authenticatedSubject.OnNext(type);
    }

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(tracking);
        }

        return _trackers[_settings.DefaultListService].Value.Update(id, tracking);
    }

    public IObservable<bool> Delete(long id)
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(false);
        }

        return _trackers[_settings.DefaultListService].Value.Delete(id);
    }
}
