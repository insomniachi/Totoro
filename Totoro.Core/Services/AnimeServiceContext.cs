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

    public ListServiceType Current => _settings.DefaultListService;

    public IObservable<IEnumerable<AnimeModel>> GetAiringAnime()
    {
        return _services[_settings.DefaultListService].GetAiringAnime();
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime(string name)
    {
        return _services[_settings.DefaultListService].GetAnime(name);
    }

    public IObservable<AnimeModel> GetInformation(long id)
    {
        return _services[_settings.DefaultListService].GetInformation(id);
    }

    public IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime()
    {
        return _services[_settings.DefaultListService].GetSeasonalAnime();
    }
}
