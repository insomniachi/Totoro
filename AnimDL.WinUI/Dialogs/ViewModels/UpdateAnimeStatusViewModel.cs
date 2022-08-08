using System;
using System.Linq;
using System.Reactive.Linq;
using MalApi;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

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
            .Select(x => x.UserStatus)
            .WhereNotNull()
            .Subscribe(x =>
            {
                Status = x.Status;
                EpisodesWatched = x.WatchedEpisodes;
                Score = x.Score;
            });

        this.ObservableForProperty(x => x.EpisodesWatched, x => x)
            .DistinctUntilChanged()
            .Where(_ => Anime is not null)
            .Where(x => x == Anime.TotalEpisodes)
            .Subscribe(x => Status = AnimeStatus.Completed);

    }


    [Reactive] public Anime Anime { get; set; }
    [Reactive] public AnimeStatus Status { get; set; }
    [Reactive] public double EpisodesWatched { get; set; }
    [Reactive] public Score Score { get; set; }
    [Reactive] public string Tags { get; set; }
    [Reactive] public Priority? Priority { get; set; }
    [Reactive] public int? RewatchCount { get; set; }
    [Reactive] public Value? RewatchValue { get; set; }
    [Reactive] public double TotalEpisodes { get; set; } 
}