namespace Totoro.Core.Models;

public interface IAnimeModel
{
    public string Title { get; set; }
    public long Id { get; set; }
    public int? TotalEpisodes { get; set; }
    public string Image { get; set; }
    public Tracking Tracking { get; set; }
}

public class AnimeModel : ReactiveObject, IAnimeModel
{
    public long Id { get; set; }
    public long? MalId { get; set; }
    public string Image { get; set; }
    public string Title { get; set; }
    [Reactive] public Tracking Tracking { get; set; }
    public int? TotalEpisodes { get; set; }
    public AiringStatus AiringStatus { get; set; }
    public float? MeanScore { get; set; }
    public int Popularity { get; set; }
    public IEnumerable<string> AlternativeTitles { get; set; } = Enumerable.Empty<string>();
    public string Description { get; set; }
    public List<Video> Videos { get; set; }
    public Season Season { get; set; }
    public IEnumerable<string> Genres { get; set; }
    public AnimeModel[] Related { get; set; } = Array.Empty<AnimeModel>();
    public AnimeModel[] Recommended { get; set; } = Array.Empty<AnimeModel>();
    public DayOfWeek? BroadcastDay { get; set; }
    [Reactive] public int AiredEpisodes { get; set; }
    [Reactive] public string BannerImage { get; set; } = string.Empty;
    [Reactive] public DateTime? NextEpisodeAt { get; set; }
    public string Type { get; set; }

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
