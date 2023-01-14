namespace Totoro.Core.Models;

public interface IAnimeModel
{
    public string Title { get; set; }
    public long Id { get; set; }
    public int? TotalEpisodes { get; set; }
    public Tracking Tracking { get; set; }
}

public class AnimeModel : ReactiveObject, IAnimeModel
{
    public long Id { get; set; }
    public string Image { get; set; }
    public string Title { get; set; }
    public Tracking Tracking { get; set; }
    public int? TotalEpisodes { get; set; }
    public AiringStatus AiringStatus { get; set; }
    public float? MeanScore { get; set; }
    public int Popularity { get; set; }
    public List<string> AlternativeTitles { get; set; } = new();
    public string Description { get; set; }
    public List<Video> Videos { get; set; }
    public Season Season { get; set; }
    public string[] Genres { get; set; }
    public AnimeModel[] Related { get; set; }
    public DayOfWeek? BroadcastDay { get; set; }
    [Reactive] public int AiredEpisodes { get; set; }

    public override string ToString()
    {
        return Title;
    }
}

public class TimeRemaining : ReactiveObject
{
    public TimeSpan TimeSpan { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    public TimeRemaining(TimeSpan timeSpan, DateTime lastUpdatedAt)
    {
        TimeSpan = timeSpan;
        LastUpdatedAt = lastUpdatedAt;
    }
}
