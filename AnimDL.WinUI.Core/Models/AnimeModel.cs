using System;
using MalApi;

namespace AnimDL.WinUI.Models;

public class AnimeModel
{
    public long Id { get; set; }
    public string Image { get; set; }
    public string Title { get; set; }
    public UserAnimeStatus UserAnimeStatus { get; set; }
    public int? TotalEpisodes { get; set; }
}

public class ScheduledAnimeModel : AnimeModel
{
    public DayOfWeek? BroadcastDay { get; set; }
    public TimeSpan? TimeToAir { get; set; }
}

public class SeasonalAnimeModel : AnimeModel
{
    public Season Season { get; set; }
}