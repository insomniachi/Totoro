namespace Totoro.Core.Models;

public record Tracking
{
    public AnimeStatus? Status { get; set; }
    public int? Score { get; set; }
    public int? WatchedEpisodes { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? FinishDate { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static Tracking Next(AnimeModel anime)
    {
        var tracking = new Tracking
        {
            WatchedEpisodes = (anime.Tracking?.WatchedEpisodes ?? 0) + 1
        };

        if (tracking.WatchedEpisodes == anime.TotalEpisodes)
        {
            tracking.Status = AnimeStatus.Completed;
            tracking.FinishDate = DateTime.Today;
        }
        else if(tracking.WatchedEpisodes == 1)
        {
            tracking.Status = AnimeStatus.Watching;
            tracking.StartDate = DateTime.Today;
        }

        return tracking;
    }
}
