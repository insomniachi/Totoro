namespace AnimDL.WinUI.Dialogs.ViewModels;

public class UpdateAnimeStatusViewModel : ReactiveObject
{
    public UpdateAnimeStatusViewModel()
    {
        this.ObservableForProperty(x => x.Anime, x => x)
            .WhereNotNull()
            .Subscribe(x => TotalEpisodes = x.TotalEpisodes ?? 0);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Select(x => x.Tracking)
            .WhereNotNull()
            .Subscribe(x =>
            {
                Status = x.Status ?? AnimeStatus.None;
                EpisodesWatched = x.WatchedEpisodes ?? 0;
                Score = x.Score ?? 0;
            });

        this.ObservableForProperty(x => x.EpisodesWatched, x => x)
            .Where(_ => Anime is not null)
            .Where(x => x == Anime.TotalEpisodes)
            .Subscribe(x => Status = AnimeStatus.Completed);

    }

    [Reactive] public AnimeModel Anime { get; set; }
    [Reactive] public AnimeStatus Status { get; set; }
    [Reactive] public double EpisodesWatched { get; set; }
    [Reactive] public int Score { get; set; }
    [Reactive] public string Tags { get; set; }
    [Reactive] public int? RewatchCount { get; set; }
    [Reactive] public double TotalEpisodes { get; set; }
}