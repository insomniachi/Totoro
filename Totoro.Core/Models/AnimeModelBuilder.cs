namespace Totoro.Core.Models;

public class MalToModelConverter
{
    private readonly ISchedule _schedule;

    public MalToModelConverter(ISchedule schedule)
    {
        _schedule = schedule;
    }

    public AnimeModel Convert<T>(MalApi.Anime anime)
        where T : AnimeModel, new()
    {
        var model = new T();
        return model switch
        {
            SeasonalAnimeModel m => PopulateSeason(m, anime),
            ScheduledAnimeModel m => PopulateSchedule(m, anime),
            AnimeModel m => Populate(m, anime),
            _ => throw new Exception("Unsupported type argument")
        };
    }

    public SearchResultModel ToSearchResult(MalApi.Anime a)
    {
        return new SearchResultModel { Title = a.Title, Id = a.Id };
    }

    private static AnimeModel Populate(AnimeModel model, MalApi.Anime malModel)
    {
        model.Id = malModel.Id;
        model.Title = malModel.Title;
        model.Image = malModel.MainPicture.Large;
        if (malModel.UserStatus is { } progress)
        {
            model.Tracking = new Tracking
            {
                WatchedEpisodes = progress.WatchedEpisodes,
                Status = (AnimeStatus)(int)progress.Status,
                Score = (int)progress.Score,
                UpdatedAt = progress.UpdatedAt,
                StartDate = progress.StartDate,
                FinishDate = progress.FinishDate
            };
        }
        model.AiringStatus = (AiringStatus)(int)(malModel.Status ?? MalApi.AiringStatus.NotYetAired);
        model.TotalEpisodes = malModel.TotalEpisodes;
        model.MeanScore = malModel.MeanScore;
        model.Popularity = malModel.Popularity ?? 0;

        if (malModel.AlternativeTitles is { } alt)
        {
            model.AlternativeTitles = alt.Aliases.ToList();
            model.AlternativeTitles.Add(alt.English);
        }

        return model;
    }

    private ScheduledAnimeModel PopulateSchedule(ScheduledAnimeModel model, MalApi.Anime malModel)
    {
        Populate(model, malModel);

        var time = _schedule.GetTimeTillEpisodeAirs(model.Id);

        if (time is null)
        {
            model.BroadcastDay = malModel.Broadcast?.DayOfWeek;
            return model;
        }

        model.BroadcastDay = (DateTime.Now + time.TimeSpan).DayOfWeek;
        model.TimeRemaining = time;
        return model;
    }

    private SeasonalAnimeModel PopulateSeason(SeasonalAnimeModel model, MalApi.Anime malModel)
    {
        Populate(model, malModel);
        if (malModel.StartSeason is { } season)
        {
            model.Season = new((AnimeSeason)(int)season.SeasonName, season.Year);
        }

        if (model.Season == CurrentSeason())
        {
            PopulateSchedule(model, malModel);
        }

        return model;
    }

    private static Season CurrentSeason()
    {
        var date = DateTime.Now;
        var year = date.Year;
        var month = date.Month;

        var current = month switch
        {
            1 or 2 or 3 => AnimeSeason.Winter,
            4 or 5 or 6 => AnimeSeason.Spring,
            7 or 8 or 9 => AnimeSeason.Summer,
            10 or 11 or 12 => AnimeSeason.Fall,
            _ => throw new InvalidOperationException()
        };

        return new(current, year);
    }

}
