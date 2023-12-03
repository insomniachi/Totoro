using System.Diagnostics;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.Simkl;

internal class SimklService : IAnimeService, ISimklService
{
    private readonly ISimklClient _simklClient;

    public SimklService(ISimklClient simklClient)
    {
        _simklClient = simklClient;
    }

    public ListServiceType Type => ListServiceType.Simkl;

    public IObservable<IEnumerable<AnimeModel>> GetAiringAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var result = await _simklClient.GetAiringAnime();
            observer.OnNext(result.Select(SimklToAnimeModelConverter.Convert));
            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime(string name)
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var result = await _simklClient.Search(name, ItemType.Anime);
            observer.OnNext(result.Select(SimklToAnimeModelConverter.Convert));
            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    public IObservable<AnimeModel> GetInformation(long id)
    {
        return Observable.Create<AnimeModel>(async observer =>
        {
            var info = await _simklClient.GetSummary(id);
            observer.OnNext(SimklToAnimeModelConverter.Convert(info));
            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime()
    {
        return Observable.Empty<IEnumerable<AnimeModel>>();
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
        if(response.FirstOrDefault() is not { Id.Simkl: not null } metaData)
        {
            return null;
        }

        var summary = await _simklClient.GetSummary(metaData.Id.Simkl.Value);
        return ToExtendedId(summary.Id);

    }

    private static AnimeIdExtended ToExtendedId(SimklIds ids)
    {
        var extended = new AnimeIdExtended() { Simkl = ids.Simkl.Value };

        if(long.TryParse(ids.MyAnimeList, out var malId))
        {
            extended.MyAnimeList = malId;
        }
        if(long.TryParse(ids.Anilist, out var anilistId))
        {
            extended.AniList = anilistId;
        }
        if(long.TryParse(ids.Kitsu, out var kitsuId))
        {
            extended.Kitsu = kitsuId;
        }
        if(long.TryParse(ids.AniDb, out var anidbId))
        {
            extended.AniDb = anidbId;
        }
        if(long.TryParse(ids.LiveChart, out var liveChartId))
        {
            extended.LiveChart = liveChartId;
        }

        return extended;
    }
}

// TODO: Can i add these properties to existing class itself ?
public record AnimeIdExtended : AnimeId
{
    public long? Simkl { get; set; }
    public long? LiveChart { get; set; }
}
