using MalApi.Interfaces;
using static Totoro.Core.Services.MyAnimeList.MalToModelConverter;

namespace Totoro.Core.Services.MyAnimeList;

public class MyAnimeListService : IAnimeService
{
    private readonly IMalClient _client;
    private readonly IAnilistService _anilistService;
    private readonly ISettings _settings;

    public MyAnimeListService(IMalClient client, 
                              IAnilistService anilistService,
                              ISettings settings)
    {
        _client = client;
        _anilistService = anilistService;
        _settings = settings;
    }

    public ListServiceType Type => ListServiceType.MyAnimeList;

    public IObservable<AnimeModel> GetInformation(long id)
    {
        return Observable.Create<AnimeModel>(async observer =>
        {
            var malModel = await _client.Anime().WithId(id)
                                        .WithFields(_commonFields)
                                        .WithField(x => x.Genres)
                                        .WithFields("related_anime{my_list_status,status}")
                                        .WithFields("recommendations{my_list_status,status}")
                                        .Find();
            
            var model = ConvertModel(malModel);
            observer.OnNext(model);

            _anilistService
                .GetBannerImage(id)
                .ToObservable()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => model.BannerImage = x);
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime(string name)
    {
        var request = _client
            .Anime()
            .WithName(name)
            .WithFields(_commonFields)
            .WithLimit(5);

        if(_settings.IncludeNsfw)
        {
            request.IncludeNsfw();
        }

        return request.Find().ToObservable().Select(x => x.Data.Select(ConvertModel));
    }

    public IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            IGetSeasonalAnimeListRequest baseRequest(MalApi.Season season)
            {
                var request = _client.Anime()
                                     .OfSeason(season.SeasonName, season.Year)
                                     .WithFields(_commonFields);

                if(_settings.IncludeNsfw)
                {
                    request.IncludeNsfw();
                }

                return request;
            }

            var current = CurrentSeason();
            var prev = PrevSeason();
            var next = NextSeason();

            try
            {
                foreach (var season in new[] { current, prev, next })
                {
                    var pagedAnime = await baseRequest(season).Find();
                    observer.OnNext(pagedAnime.Data.Select(MalToModelConverter.ConvertModel));
                }

                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }

            return Disposable.Empty;
        });
    }


    public IObservable<IEnumerable<AnimeModel>> GetAiringAnime()
    {
        var request = _client
            .Anime()
            .Top(MalApi.AnimeRankingType.Airing)
            .IncludeNsfw()
            .WithFields(_commonFields);

        if (_settings.IncludeNsfw)
        {
            request.IncludeNsfw();
        }

        return request.Find()
                      .ToObservable()
                      .Select(x => x.Data.Select(x => ConvertModel(x.Anime)));
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
        MalApi.AnimeFieldNames.StartDate
    };
}
