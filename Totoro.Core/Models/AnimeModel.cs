using System.Diagnostics;

namespace Totoro.Core.Models;

public interface IAnimeModel
{
    public string Title { get; set; }
    public long Id { get; set; }
    public int? TotalEpisodes { get; set; }
    public string Image { get; set; }
    public Tracking Tracking { get; set; }
}

[DebuggerDisplay("{Title}")]
public class AnimeModel : ReactiveObject, IAnimeModel
{
    public long Id { get; set; }
    public long? MalId { get; set; }
    public string Image { get; set; }
    public string Title { get; set; }
    public string EngTitle { get; set; }
    public string RomajiTitle { get; set; }
    [Reactive] public Tracking Tracking { get; set; }
    public int? TotalEpisodes { get; set; }
    public AiringStatus AiringStatus { get; set; }
    public float? MeanScore { get; set; }
    public int Popularity { get; set; }
    public IEnumerable<string> AlternativeTitles { get; set; } = [];
    public string Description { get; set; }
    public List<Video> Videos { get; set; }
    public Season Season { get; set; }
    public IEnumerable<string> Genres { get; set; }
    public AnimeModel[] Related { get; set; } = [];
    public AnimeModel[] Recommended { get; set; } = [];
    public DayOfWeek? BroadcastDay { get; set; }
    public string Type { get; set; }
    [Reactive] public int AiredEpisodes { get; set; }
    [Reactive] public string BannerImage { get; set; } = string.Empty;
    [Reactive] public DateTime? NextEpisodeAt { get; set; }
}

public class TimeRemaining(TimeSpan timeSpan, DateTime lastUpdatedAt) : ReactiveObject
{
    public TimeSpan TimeSpan { get; set; } = timeSpan;
    public DateTime LastUpdatedAt { get; set; } = lastUpdatedAt;
}
