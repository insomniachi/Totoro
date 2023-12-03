using System.Diagnostics;

namespace Totoro.Core.Services.Simkl;

internal class SimklService : ISimklService
{
    private readonly ISimklClient _simklClient;

    public SimklService(ISimklClient simklClient)
    {
        _simklClient = simklClient;
    }

    public async Task<AnimeIdExtended> GetId(ListServiceType type, long id)
    {
        var serviceType = type switch
        {
            ListServiceType.MyAnimeList => "mal",
            ListServiceType.AniList => "anilist",
            ListServiceType.Kitsu => "kitsu",
            ListServiceType.AniDb => "anidb",
            ListServiceType.Simkl => "simkl",
            _ => throw new UnreachableException()
        };

        var response = await _simklClient.Search(serviceType, id);
        if (response.FirstOrDefault() is not { Id.Simkl: not null } metaData)
        {
            return null;
        }

        var summary = await _simklClient.GetSummary(metaData.Id.Simkl.Value);
        return ToExtendedId(summary.Id);

    }

    private static AnimeIdExtended ToExtendedId(SimklIds ids)
    {
        var extended = new AnimeIdExtended() { Simkl = ids.Simkl.Value };

        if (long.TryParse(ids.MyAnimeList, out var malId))
        {
            extended.MyAnimeList = malId;
        }
        if (long.TryParse(ids.Anilist, out var anilistId))
        {
            extended.AniList = anilistId;
        }
        if (long.TryParse(ids.Kitsu, out var kitsuId))
        {
            extended.Kitsu = kitsuId;
        }
        if (long.TryParse(ids.AniDb, out var anidbId))
        {
            extended.AniDb = anidbId;
        }
        if (long.TryParse(ids.LiveChart, out var liveChartId))
        {
            extended.LiveChart = liveChartId;
        }

        return extended;
    }
}
