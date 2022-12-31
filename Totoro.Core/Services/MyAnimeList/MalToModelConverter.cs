using System.Globalization;
using Splat;

namespace Totoro.Core.Models;

public class MalToModelConverter
{
    public static AnimeModel ConvertModel(MalApi.Anime malModel)
    {
        var model = new AnimeModel
        {
            Id = malModel.Id,
            Title = malModel.Title,
            Image = malModel.MainPicture.Large,
            Description = malModel.Synopsis
        };

        try
        {
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
            if(DateTime.TryParseExact(malModel.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                model.BroadcastDay = dt.DayOfWeek;
            }

            if (malModel.AlternativeTitles is { } alt)
            {
                model.AlternativeTitles = alt.Aliases.ToList();
                model.AlternativeTitles.Add(alt.English);
                model.AlternativeTitles.Add(alt.Japanese);
            }

            if (malModel.Videos is { Length: > 0 } videos)
            {
                model.Videos = malModel.Videos.Select(x => new Video
                {
                    Id = x.Id,
                    Thumbnail = x.Thumbnail,
                    Title = x.Title,
                    Url = x.Url,
                }).ToList();
            }

            if (malModel.StartSeason is { } season)
            {
                model.Season = new((AnimeSeason)(int)season.SeasonName, season.Year);
            }

            if (malModel.Genres is { Length: > 0 } g)
            {
                model.Genres = g.Select(x => x.Name).ToArray();
            }

            if (malModel.RelatedAnime is { Length: > 0 } ra)
            {
                model.Related = ra.Select(x => ConvertModel(x.Anime)).ToArray();
            }
        }
        catch (Exception ex)
        {
            Locator.Current.GetService<ILogManager>().GetLogger<MalToModelConverter>().Error(ex);
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
