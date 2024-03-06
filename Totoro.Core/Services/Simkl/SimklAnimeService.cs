using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.Simkl;

internal class SimklAnimeService(ISimklClient simklClient,
                                 IAnilistService anilistService,
                                 IConfiguration configuration) : IAnimeService
{
    private readonly ISimklClient _simklClient = simklClient;
    private readonly IAnilistService _anilistService = anilistService;
    private readonly string _clientId = configuration["ClientIdSimkl"];

    public ListServiceType Type => ListServiceType.Simkl;

    public async IAsyncEnumerable<AnimeModel> GetAiringAnime()
    {
        var result = await _simklClient.GetAiringAnime(_clientId);
        foreach (var item in result.Select(SimklToAnimeModelConverter.Convert))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<AnimeModel> GetAnime(string name)
    {
        var result = await _simklClient.Search(name, ItemType.Anime, _clientId);
        foreach (var item in result.Select(SimklToAnimeModelConverter.Convert))
        {
            yield return item;
        }
    }

    public async Task<AnimeModel> GetInformation(long id)
    {
        var info = await _simklClient.GetSummary(id, _clientId);
        var model = SimklToAnimeModelConverter.Convert(info);
            
        _anilistService
            .GetBannerImage(id)
            .ToObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => model.BannerImage = x);

        return model;
    }

    public IAsyncEnumerable<AnimeModel> GetSeasonalAnime()
    {
        return AsyncEnumerable.Empty<AnimeModel>();
    }
}

// TODO: Can i add these properties to existing class itself ?
public record AnimeIdExtended : AnimeId
{
    public long? Simkl { get; set; }
    public long? LiveChart { get; set; }

    public bool HasId(ListServiceType type)
    {
        return type switch
        {
            ListServiceType.AniDb => AniDb is not null,
            ListServiceType.AniList => AniList is not null,
            ListServiceType.MyAnimeList => MyAnimeList is not null,
            ListServiceType.Kitsu => Kitsu is not null,
            ListServiceType.Simkl => Simkl is not null,
            _ => false
        };
    }

    public bool IsEmpty() => this is { AniDb: null, AniList: null, MyAnimeList: null, Kitsu: null, LiveChart: null, Simkl: null };  
}
