namespace Totoro.Core.Services;

public class AnimeServiceContext : IAnimeServiceContext
{
    private readonly ISettings _settings;
    private readonly Dictionary<ListServiceType, IAnimeService> _services;

    public AnimeServiceContext(ISettings settings,
                               IEnumerable<IAnimeService> services)
    {
        _settings = settings;
        _services = services.Any()
            ? services.ToDictionary(x => x.Type, x => x)
            : new();
    }

    public ListServiceType? Current => _settings.DefaultListService;

    public IObservable<IEnumerable<AnimeModel>> GetAiringAnime()
    {
        if (_settings.DefaultListService is not ListServiceType type)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _services[type].GetAiringAnime();
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime(string name)
    {
        if (_settings.DefaultListService is not ListServiceType type)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _services[type].GetAnime(name);
    }

    public IObservable<AnimeModel> GetInformation(long id)
    {
        if (_settings.DefaultListService is not ListServiceType type)
        {
            return Observable.Empty<AnimeModel>();
        }

        return _services[type].GetInformation(id);
    }

    public IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime()
    {
        if (_settings.DefaultListService is not ListServiceType type)
        {
            return Observable.Return(Enumerable.Empty<AnimeModel>());
        }

        return _services[type].GetSeasonalAnime();
    }
}
