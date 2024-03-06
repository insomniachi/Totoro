using Microsoft.Extensions.DependencyInjection;

namespace Totoro.Core.Services;

public class AnimeServiceContext : IAnimeServiceContext
{
    private readonly ISettings _settings;
    private readonly IConnectivityService _connectivityService;
    private readonly Dictionary<ListServiceType, Lazy<IAnimeService>> _services;

    public AnimeServiceContext(ISettings settings,
                               [FromKeyedServices(ListServiceType.MyAnimeList)] Lazy<IAnimeService> myAnimeListService,
                               [FromKeyedServices(ListServiceType.AniList)] Lazy<IAnimeService> anilistService,
                               [FromKeyedServices(ListServiceType.Simkl)] Lazy<IAnimeService> simklService,
                               IConnectivityService connectivityService)
    {
        _settings = settings;
        _connectivityService = connectivityService;
        _services = new()
        {
            { ListServiceType.MyAnimeList, myAnimeListService },
            { ListServiceType.AniList, anilistService },
            { ListServiceType.Simkl, simklService }
        };
    }

    public ListServiceType Current => _settings.DefaultListService;

    public IAsyncEnumerable<AnimeModel> GetAiringAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return AsyncEnumerable.Empty<AnimeModel>();
        }

        return _services[_settings.DefaultListService].Value.GetAiringAnime();
    }

    public IAsyncEnumerable<AnimeModel> GetAnime(string name)
    {
        if (!_connectivityService.IsConnected)
        {
            return AsyncEnumerable.Empty<AnimeModel>();
        }

        return _services[_settings.DefaultListService].Value.GetAnime(name);
    }

    public async Task<AnimeModel> GetInformation(long id)
    {
        if (!_connectivityService.IsConnected)
        {
            return new();
        }

        return await _services[_settings.DefaultListService].Value.GetInformation(id);
    }

    public IAsyncEnumerable<AnimeModel> GetSeasonalAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return AsyncEnumerable.Empty<AnimeModel>();
        }

        return _services[_settings.DefaultListService].Value.GetSeasonalAnime();
    }
}
