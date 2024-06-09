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
    
    public IAsyncEnumerable<AnimeModel> GetAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return AsyncEnumerable.Empty<AnimeModel>();
        }

        return _trackers[_settings.DefaultListService].Value.GetAnime();
    }

    public IAsyncEnumerable<AnimeModel> GetCurrentlyAiringTrackedAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return AsyncEnumerable.Empty<AnimeModel>();
        }

        return _trackers[_settings.DefaultListService].Value.GetCurrentlyAiringTrackedAnime();
    }

    public void SetAccessToken(string token, ListServiceType type)
    {
        _trackers[type].Value.SetAccessToken(token);
        _authenticatedSubject.OnNext(type);
    }

    public async Task<Tracking> Update(long id, Tracking tracking)
    {
        if (!_connectivityService.IsConnected)
        {
            return tracking;
        }

        return await _trackers[_settings.DefaultListService].Value.Update(id, tracking);
    }

    public async Task<bool> Delete(long id)
    {
        if (!_connectivityService.IsConnected)
        {
            return false;
        }

        return await _trackers[_settings.DefaultListService].Value.Delete(id);
    }

    public async Task<User> GetUser()
    {
        if (!_connectivityService.IsConnected)
        {
            return new User();
        }

        return await _trackers[_settings.DefaultListService].Value.GetUser();
    }
}
