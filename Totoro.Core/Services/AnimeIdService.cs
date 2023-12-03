using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Services;

public class AnimeIdService : IAnimeIdService
{
    private readonly ISettings _settings;
    private readonly ISimklService _simklService;

    public AnimeIdService(ISettings settings,
                          ISimklService simklService)
    {
        _settings = settings;
        _simklService = simklService;
    }

    public Task<AnimeIdExtended> GetId(ListServiceType serviceType, long id)
    {
        return _simklService.GetId(serviceType, id);
    }

    public Task<AnimeIdExtended> GetId(long id)
    {
        return _simklService.GetId(_settings.DefaultListService, id);
    }
}
