namespace Totoro.Core.Services;

public class AnimeServiceContext : IAnimeServiceContext
{
    private readonly ISettings _settings;
    private readonly IConnectivityService _connectivityService;
    private readonly Dictionary<ListServiceType, IAnimeService> _services;

    public AnimeServiceContext(ISettings settings,
                               IEnumerable<IAnimeService> services,
                               IConnectivityService connectivityService)
    {
        _settings = settings;
        _connectivityService = connectivityService;
        _services = services.Any()
            ? services.ToDictionary(x => x.Type, x => x)
            : new();
    }

    public ListServiceType Current => _settings.DefaultListService;

    public IObservable<IEnumerable<AnimeModel>> GetAiringAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _services[_settings.DefaultListService].GetAiringAnime();
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime(string name)
    {
        if(!_connectivityService.IsConnected)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _services[_settings.DefaultListService].GetAnime(name);
    }

    public IObservable<AnimeModel> GetInformation(long id)
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(new AnimeModel());
        }

        return _services[_settings.DefaultListService].GetInformation(id);
    }

    public IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime()
    {
        if (!_connectivityService.IsConnected)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _services[_settings.DefaultListService].GetSeasonalAnime();
    }
}
