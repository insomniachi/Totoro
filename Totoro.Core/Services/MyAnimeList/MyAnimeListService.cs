using JikanDotNet;
using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.Configuration;
using static Totoro.Core.Services.MyAnimeList.MalToModelConverter;

namespace Totoro.Core.Services.MyAnimeList;

public class MyAnimeListService : IAnimeService, IMyAnimeListService
{
    private readonly IMalClient _client;
    private readonly IAnilistService _anilistService;
    private readonly IAnimeIdService _animeIdService;
    private readonly ISettings _settings;
    private readonly IJikan _jikan = new Jikan();
    private readonly string _recursiveAnimeProperties = $"my_list_status,status,{AnimeFieldNames.TotalEpisodes},{AnimeFieldNames.Mean}";

    public MyAnimeListService(IMalClient client,
                              IAnilistService anilistService,
                              IAnimeIdService animeIdService,
                              ISettings settings,
                              IConfiguration configuration,
                              ILocalSettingsService localSettingsService)
    {
        _client = client;
        _anilistService = anilistService;
        _animeIdService = animeIdService;
        _settings = settings;
        
        var token = localSettingsService.ReadSetting<MalApi.OAuthToken>("MalToken");
        var clientId = configuration["ClientId"];
        if ((DateTime.UtcNow - (token?.CreateAt ?? DateTime.UtcNow)).Days >= 28)
        {
            token = MalApi.MalAuthHelper.RefreshToken(clientId, token!.RefreshToken).Result;
            localSettingsService.SaveSetting("MalToken", token);
        }
        if (token is not null && !string.IsNullOrEmpty(token.AccessToken))
        {
            client.SetAccessToken(token.AccessToken);
        }
        client.SetClientId(clientId);
    }

    public ListServiceType Type => ListServiceType.MyAnimeList;

    public async Task<AnimeModel> GetInformation(long id)
    {
        var malModel = await _client.Anime().WithId(id)
                                    .WithFields(_commonFields)
                                    .WithField(x => x.Genres)
                                    .WithFields($"related_anime{{{_recursiveAnimeProperties}}}")
                                    .WithFields($"recommendations{{{_recursiveAnimeProperties}}}")
                                    .Find();

        var model = ConvertModel(malModel);

        _anilistService
            .GetBannerImage(id)
            .ToObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => model.BannerImage = x);

        return model;
    }

    public async IAsyncEnumerable<AnimeModel> GetAnime(string name)
    {
        var request = _client
            .Anime()
            .WithName(name)
            .WithFields(_commonFields)
            .WithLimit(5);

        if (_settings.IncludeNsfw)
        {
            request.IncludeNsfw();
        }

        var result = await request.Find();
        foreach (var item in result.Data.Select(ConvertModel))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<AnimeModel> GetSeasonalAnime()
    {
        IGetSeasonalAnimeListRequest baseRequest(MalApi.Season season)
        {
            var request = _client.Anime()
                                 .OfSeason(season.SeasonName, season.Year)
                                 .WithFields(_commonFields);

            if (_settings.IncludeNsfw)
            {
                request.IncludeNsfw();
            }

            return request;
        }

        var current = CurrentSeason();
        var prev = PrevSeason();
        var next = NextSeason();

        foreach (var season in new[] { current, prev, next })
        {
            var pagedAnime = await baseRequest(season).Find();

            foreach (var item in pagedAnime.Data.Select(ConvertModel))
            {
                yield return item;
            }
        }
    }


    public IAsyncEnumerable<AnimeModel> GetAiringAnime() => GetTopAnime(AnimeRankingType.Airing);
    
    public IAsyncEnumerable<AnimeModel> GetUpcomingAnime() => GetTopAnime(AnimeRankingType.Upcoming);
    
    public IAsyncEnumerable<AnimeModel> GetPopularAnime() => GetTopAnime(AnimeRankingType.ByPopularity);
    
    public async IAsyncEnumerable<AnimeModel> GetRecommendedAnime()
    {
        var request = _client
            .Anime()
            .SuggestedForMe()
            .WithLimit(15)
            .IncludeNsfw()
            .WithFields(_commonFields);

        if (_settings.IncludeNsfw)
        {
            request.IncludeNsfw();
        }


        var result = await request.Find();

        foreach (var item in result.Data.Select(ConvertModel))
        {
            yield return item;
        }
    }

    private async IAsyncEnumerable<AnimeModel> GetTopAnime(AnimeRankingType type)
    {
        var request = _client
            .Anime()
            .Top(type)
            .WithLimit(15)
            .IncludeNsfw()
            .WithFields(_commonFields);

        if (_settings.IncludeNsfw)
        {
            request.IncludeNsfw();
        }

        var result = await request.Find();

        foreach (var item in result.Data.Select(x => ConvertModel(x.Anime)))
        {
            yield return item;
        }
    }

    public async Task<IEnumerable<EpisodeModel>> GetEpisodes(long id)
    {
        try
        {
            var response = await _jikan.GetAnimeEpisodesAsync(id);
            return response.Data.Select((x, index) => new EpisodeModel
            {
                EpisodeNumber = index + 1,
                EpisodeTitle = x.Title
            });
        }
        catch
        {
            return [];
        }
    }

    public async IAsyncEnumerable<int> GetFillers(long id)
    {
        var animeId = await _animeIdService.GetId(id);

        if (animeId is null || animeId.MyAnimeList is null)
        {
            yield break;
        }

        PaginatedJikanResponse<ICollection<AnimeEpisode>> response = null;
        var currentPage = 1;
        var offset = 0;
        do
        {
            try
            {
                response = await _jikan.GetAnimeEpisodesAsync(animeId.MyAnimeList.Value, currentPage++);
            }
            catch
            {
                yield break;
            }

            foreach (var item in response.Data.Select((x, index) => (x, index)).Where(x => x.x.Filler == true).Select(x => x.index + 1 + offset))
            {
                yield return item;
            }

            offset += response.Data.Count;

        } while (response.Pagination.HasNextPage);
    }

    private static MalApi.Season PrevSeason()
    {
        var date = DateTime.Now;
        var year = date.Year;
        var month = date.Month;

        var current = month switch
        {
            1 or 2 or 3 => MalApi.AnimeSeason.Fall,
            4 or 5 or 6 => MalApi.AnimeSeason.Winter,
            7 or 8 or 9 => MalApi.AnimeSeason.Spring,
            10 or 11 or 12 => MalApi.AnimeSeason.Summer,
            _ => throw new InvalidOperationException()
        };

        if (month is 1 or 2 or 3)
        {
            year--;
        }

        return new(current, year);
    }

    private static MalApi.Season NextSeason()
    {
        var date = DateTime.Now;
        var year = date.Year;
        var month = date.Month;

        var current = month switch
        {
            1 or 2 or 3 => MalApi.AnimeSeason.Spring,
            4 or 5 or 6 => MalApi.AnimeSeason.Summer,
            7 or 8 or 9 => MalApi.AnimeSeason.Fall,
            10 or 11 or 12 => MalApi.AnimeSeason.Winter,
            _ => throw new InvalidOperationException()
        };

        if (month is 10 or 11 or 12)
        {
            year++;
        }

        return new(current, year);
    }

    private static MalApi.Season CurrentSeason()
    {
        var date = DateTime.Now;
        var year = date.Year;
        var month = date.Month;

        var current = month switch
        {
            1 or 2 or 3 => MalApi.AnimeSeason.Winter,
            4 or 5 or 6 => MalApi.AnimeSeason.Spring,
            7 or 8 or 9 => MalApi.AnimeSeason.Summer,
            10 or 11 or 12 => MalApi.AnimeSeason.Fall,
            _ => throw new InvalidOperationException()
        };

        return new(current, year);
    }

    private readonly string[] _commonFields = new string[]
    {
        MalApi.AnimeFieldNames.Synopsis,
        MalApi.AnimeFieldNames.TotalEpisodes,
        MalApi.AnimeFieldNames.Broadcast,
        MalApi.AnimeFieldNames.UserStatus,
        MalApi.AnimeFieldNames.NumberOfUsers,
        MalApi.AnimeFieldNames.Rank,
        MalApi.AnimeFieldNames.Mean,
        MalApi.AnimeFieldNames.AlternativeTitles,
        MalApi.AnimeFieldNames.Popularity,
        MalApi.AnimeFieldNames.StartSeason,
        MalApi.AnimeFieldNames.Genres,
        MalApi.AnimeFieldNames.Status,
        MalApi.AnimeFieldNames.Videos,
        MalApi.AnimeFieldNames.StartDate,
        MalApi.AnimeFieldNames.MediaType
    };
}
