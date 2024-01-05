using Totoro.Core.Services.Simkl;

namespace Totoro.Core.Services;

public class AnimeIdService(ISettings settings,
                            ISimklService simklService,
                            IOfflineAnimeIdService offlineIdMap) : IAnimeIdService
{
    private readonly ISettings _settings = settings;
    private readonly ISimklService _simklService = simklService;
    private readonly IOfflineAnimeIdService _offlineIdMap = offlineIdMap;

    public async ValueTask<AnimeIdExtended> GetId(ListServiceType from, ListServiceType to, long id)
    {
        if(_offlineIdMap.GetId(from, to, id) is not { } idExtended)
        {
            return await _simklService.GetId(from, id);
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
