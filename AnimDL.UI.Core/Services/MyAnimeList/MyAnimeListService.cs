﻿using MalApi.Interfaces;

namespace AnimDL.UI.Core.Services.MyAnimeList;

public class MyAnimeListService : IAnimeService
{
    private readonly IMalClient _client;
    private readonly MalToModelConverter _converter;

    public MyAnimeListService(IMalClient client,
                              MalToModelConverter converter)
    {
        _client = client;
        _converter = converter;
    }

    public IObservable<AnimeModel> GetInformation(long id)
    {
        return _client.Anime().WithId(id).WithFields(_fields).Find()
                      .ToObservable()
                      .Select(malModel => _converter.Convert<AnimeModel>(malModel));
    }

    public IObservable<IEnumerable<SeasonalAnimeModel>> GetSeasonalAnime()
    {
        return Observable.Create<IEnumerable<SeasonalAnimeModel>>(async observer =>
        {
            IGetSeasonalAnimeListRequest baseRequest(MalApi.Season season)
            {
                return _client.Anime().OfSeason(season.SeasonName, season.Year)
                              .WithField(x => x.UserStatus).WithField(x => x.Broadcast)
                              .WithField(x => x.StartSeason).WithField(x => x.TotalEpisodes)
                              .WithField(x => x.MeanScore).WithField(x => x.Popularity);
            }

            var current = CurrentSeason();
            var prev = PrevSeason();
            var next = NextSeason();

            try
            {
                foreach (var season in new[] { current, prev, next })
                {
                    var pagedAnime = await baseRequest(season).Find();
                    observer.OnNext(pagedAnime.Data.Select(malModel => _converter.Convert<SeasonalAnimeModel>(malModel) as SeasonalAnimeModel));
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


    private readonly string[] _fields = new string[]
    {
        MalApi.AnimeFieldNames.Synopsis,
        MalApi.AnimeFieldNames.TotalEpisdoes,
        MalApi.AnimeFieldNames.Broadcast,
        MalApi.AnimeFieldNames.UserStatus,
        MalApi.AnimeFieldNames.NumberOfUsers,
        MalApi.AnimeFieldNames.Rank,
        MalApi.AnimeFieldNames.Mean,
    };
}