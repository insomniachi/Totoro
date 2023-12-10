using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Services;

public class AnimeIdService : IAnimeIdService
{
    private readonly ISettings _settings;
    private readonly ISimklService _simklService;
    private readonly IOfflineAnimeIdService _offlineIdMap;

    public AnimeIdService(ISettings settings,
                          ISimklService simklService,
                          IOfflineAnimeIdService offlineIdMap)
    {
        _settings = settings;
        _simklService = simklService;
        _offlineIdMap = offlineIdMap;
    }

    public async ValueTask<AnimeIdExtended> GetId(ListServiceType serviceType, long id)
    {
        if(_offlineIdMap.GetId(serviceType, id) is not { } idExtended)
        {
            return await _simklService.GetId(serviceType, id);
        }

        return idExtended;
    }

    public async ValueTask<AnimeIdExtended> GetId(long id)
    {
        if (_offlineIdMap.GetId(id) is not { } idExtended)
        {
            return await _simklService.GetId(_settings.DefaultListService, id);
        }

        return idExtended;
    }
}
