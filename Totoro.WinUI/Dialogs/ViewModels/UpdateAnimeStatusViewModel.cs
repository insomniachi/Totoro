namespace Totoro.WinUI.Dialogs.ViewModels;

public class UpdateAnimeStatusViewModel : ReactiveObject
{
    private readonly ITrackingServiceContext _trackingService;

    public UpdateAnimeStatusViewModel(ITrackingServiceContext trackingService)
    {
        _trackingService = trackingService;

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

    public void UpdateTracking()
    {
        if(Anime is null)
        {
            return;
        }

        var tracking = new Tracking();

        if (Anime.Tracking?.Status != Status)
        {
            tracking.Status = Status;
        }
        if (Score is int score && score != (Anime.Tracking?.Score ?? 0))
        {
            tracking.Score = score;
        }
        if (EpisodesWatched > 0)
        {
            tracking.WatchedEpisodes = (int)EpisodesWatched;
        }
        if (StartDate is { } sd)
        {
            tracking.StartDate = sd.Date;
        }
        if (FinishDate is { } fd)
        {
            tracking.FinishDate = fd.Date;
        }

        _trackingService.Update(Anime.Id, tracking).Subscribe(t => Anime.Tracking = t);
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