using System.Diagnostics;

namespace Totoro.Core.Services.Simkl;

internal class SimklService : ISimklService
{
    private readonly ISimklClient _simklClient;
    private readonly ISettings _settings;

    public SimklService(ISimklClient simklClient,
                        ISettings settings)
    {
        _simklClient = simklClient;
        _settings = settings;
    }

    public async Task<AnimeIdExtended> GetId(ListServiceType type, long id)
    {
        var response = await _simklClient.Search(GetServiceType(type), id);
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

    public async Task<IEnumerable<EpisodeModel>> GetEpisodes(long id)
    {
        id = await GetSimklId(id);

        if(id == 0)
        {
            return Enumerable.Empty<EpisodeModel>();
        }

        var episodes = await _simklClient.GetEpisodes(id);

        return episodes.Select(x => new EpisodeModel
        {
            EpisodeNumber = x.EpisodeNumber,
            EpisodeTitle = x.Title
        });
    }

    private async ValueTask<long> GetSimklId(long id)
    {
        if(_settings.DefaultListService == ListServiceType.Simkl)
        {
            return id;
        }

        try
        {
            var response = await _simklClient.Search(GetServiceType(_settings.DefaultListService), id);
            if (response.FirstOrDefault() is not { Id.Simkl: not null } metaData)
            {
                return 0;
            }

            return metaData.Id.Simkl ?? 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string GetServiceType(ListServiceType type)
    {
        return type switch
        {
            ListServiceType.MyAnimeList => "mal",
            ListServiceType.AniList => "anilist",
            ListServiceType.Kitsu => "kitsu",
            ListServiceType.AniDb => "anidb",
            ListServiceType.Simkl => "simkl",
            _ => throw new UnreachableException()
        };
    }
}
