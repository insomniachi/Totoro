namespace Totoro.Core.Helpers;

public class AnimeHelpers
{
    public static Season PrevSeason()
    {
        var date = DateTime.Now;
        var year = date.Year;
        var month = date.Month;

        var current = month switch
        {
            1 or 2 or 3 => AnimeSeason.Fall,
            4 or 5 or 6 => AnimeSeason.Winter,
            7 or 8 or 9 => AnimeSeason.Spring,
            10 or 11 or 12 => AnimeSeason.Summer,
            _ => throw new InvalidOperationException()
        };

        if (month is 1 or 2 or 3)
        {
            year--;
        }

        return new(current, year);
    }

    public static Season NextSeason()
    {
        var date = DateTime.Now;
        var year = date.Year;
        var month = date.Month;

        var current = month switch
        {
            1 or 2 or 3 => AnimeSeason.Spring,
            4 or 5 or 6 => AnimeSeason.Summer,
            7 or 8 or 9 => AnimeSeason.Fall,
            10 or 11 or 12 => AnimeSeason.Winter,
            _ => throw new InvalidOperationException()
        };

        if (month is 10 or 11 or 12)
        {
            year++;
        }

        return new(current, year);
    }

    public static Season CurrentSeason()
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

    public static double ScoreToRating(int? score) => score is > 0 ? score.Value / 2.0 : -1;
    public static string Progress(Tracking tracking, int? totalEpisodes)
    {
        var watched = (tracking?.WatchedEpisodes ?? 0) == 0
            ? "-" : tracking.WatchedEpisodes.ToString();
        var total = (totalEpisodes == 0 ? "??" : totalEpisodes.ToString());
        return $"{watched}/{total}";
    }

    public static string UserScore(AnimeModel a)
    {
        if (a.Tracking is null)
        {
            return "-";
        }

        return a.Tracking.Score == 0
            ? "-"
            : a.Tracking.Score.ToString();
    }
}
