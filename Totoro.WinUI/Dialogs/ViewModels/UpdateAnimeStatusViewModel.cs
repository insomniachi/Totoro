namespace Totoro.WinUI.Dialogs.ViewModels;

public class UpdateAnimeStatusViewModel : ReactiveObject
{
    public UpdateAnimeStatusViewModel()
    {
        this.ObservableForProperty(x => x.Anime, x => x)
            .WhereNotNull()
            .Subscribe(x => TotalEpisodes = x.TotalEpisodes == 0 ? double.MaxValue : x.TotalEpisodes.Value);

        this.ObservableForProperty(x => x.Anime, x => x)
            .WhereNotNull()
            .Select(x => x.Tracking)
            .WhereNotNull()
            .Subscribe(x =>
            {
                Status = x.Status ?? AnimeStatus.None;
                EpisodesWatched = x.WatchedEpisodes ?? 0;
                Score = x.Score ?? 0;

                if (x.StartDate != new DateTime())
                {
                    StartDate = x.StartDate;
                }

                if (x.FinishDate != new DateTime())
                {
                    FinishDate = x.FinishDate;
                }

            });

        this.ObservableForProperty(x => x.EpisodesWatched, x => x)
            .Where(_ => Anime is not null)
            .Where(x => x == Anime.TotalEpisodes)
            .Subscribe(x => Status = AnimeStatus.Completed);

    }

    [Reactive] public IAnimeModel Anime { get; set; }
    [Reactive] public AnimeStatus Status { get; set; } = AnimeStatus.PlanToWatch;
    [Reactive] public double EpisodesWatched { get; set; }
    [Reactive] public int Score { get; set; }
    [Reactive] public string Tags { get; set; }
    [Reactive] public int? RewatchCount { get; set; }
    [Reactive] public double TotalEpisodes { get; set; }
    [Reactive] public DateTimeOffset? StartDate { get; set; }
    [Reactive] public DateTimeOffset? FinishDate { get; set; }
}