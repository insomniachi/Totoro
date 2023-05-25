using System.Reactive.Concurrency;
using Totoro.Core.Torrents;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.ViewModels;

public class AboutAnimeViewModel : NavigatableViewModel
{
    public ObservableCollection<PivotItemModel> Pages { get; } = new()
    {
        new PivotItemModel { Header = "Previews" },
        new PivotItemModel { Header = "Related" },
        new PivotItemModel { Header = "Recommended" },
        new PivotItemModel { Header = "OST" },
        new PivotItemModel { Header = "Torrents" }
    };

    public AboutAnimeViewModel(IAnimeServiceContext animeService,
                               IViewService viewService,
                               IAnimeSoundsService animeSoundService,
                               ITorrentCatalogFactory torrentCatalogFactory,
                               ISettings settings,
                               IMyAnimeListService myAnimeListService,
                               IAnimeIdService animeIdService,
                               IDebridServiceContext debridServiceContext)
    {

        if(PluginFactory<AnimeProvider>.Instance.Plugins.FirstOrDefault(x => x.Name == settings.DefaultProviderType) is { } provider)
        {
            DefaultProviderType = $"({provider.DisplayName})";
        }

        PlaySound = ReactiveCommand.Create<AnimeSound>(sound => viewService.PlayVideo(sound.SongName, sound.Url));
        Pause = ReactiveCommand.Create(animeSoundService.Pause);

        this.ObservableForProperty(x => x.Id, x => x)
            .Where(id => id > 0)
            .SelectMany(animeService.GetInformation)
            .Subscribe(x => Anime = x);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Select(anime => anime.Tracking is { })
            .ToPropertyEx(this, x => x.HasTracking, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Select(anime => anime.Id)
            .Where(id => id > 0)
            .Select(animeSoundService.GetThemes)
            .ToPropertyEx(this, x => x.Sounds, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue (x => x.Episodes, x => x.Anime)
            .Where(x => x is not (_, null))
            .Select(x => x.Item1 is { Count : > 0} || x.Item2.AiringStatus is not AiringStatus.NotYetAired)
            .ToPropertyEx(this, x => x.CanWatch, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.Sounds)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(sounds =>
            {
                if (sounds is { Count: > 0 })
                {
                    return;
                }

                Pages.Remove(Pages.First(x => x.Header == "OST"));

            });

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async anime =>
            {
                if(anime.Videos is not { Count : >0})
                {
                    Pages.Remove(Pages.First(x => x.Header == "Previews"));
                }
                if(anime.Related is not { Length: > 0 })
                {
                    Pages.Remove(Pages.First(x => x.Header == "Related"));
                }
                if (anime.Recommended is not { Length: > 0 })
                {
                    Pages.Remove(Pages.First(x => x.Header == "Recommended"));
                }

                ObservableCollection<EpisodeModel> episodes = new ((await myAnimeListService.GetEpisodes((await animeIdService.GetId(anime.Id)).MyAnimeList)));
                var lastEpisodeNumber = episodes.LastOrDefault()?.EpisodeNumber ?? 0;
                var count = anime.AiredEpisodes - lastEpisodeNumber;
                if(count > 0)
                {
                    foreach (var ep in Enumerable.Range(lastEpisodeNumber + 1, count))
                    {
                        episodes.Add(new EpisodeModel { EpisodeNumber = ep });
                    }
                }

                if(episodes.Count == 0)
                {
                    Pages.Remove(Pages.First(x => x.Header == "Torrents"));
                }

                Episodes = episodes;
                SelectedEpisode = Episodes.FirstOrDefault(x => x.EpisodeNumber == (anime.Tracking?.WatchedEpisodes ?? 0) + 1) ?? Episodes.LastOrDefault();
            });

        this.WhenAnyValue(x => x.SelectedPage)
            .Where(x => x is null && Pages.Any(x => x.Visible))
            .Subscribe(_ => SelectedPage = Pages.First(x => x.Visible));

        this.WhenAnyValue(x => x.SelectedEpisode)
            .WhereNotNull()
            .Subscribe(episode =>
            {
                var catalog = torrentCatalogFactory.GetCatalog(settings.TorrentProviderType);
                RxApp.MainThreadScheduler.Schedule(async () =>
                {
                    IsLoading = true;
                    Torrents = await catalog.Search($"{Anime.Title} - {(episode.EpisodeNumber).ToString().PadLeft(2, '0')}").ToListAsync();
                    var index = 0;
                    await foreach (var item in debridServiceContext.Check(Torrents.Select(x => x.MagnetLink)))
                    {
                        Torrents[index++].State = item ? TorrentState.Cached : TorrentState.NotCached;
                    }
                    IsLoading = false;
                });
            });
    }

    [Reactive] public long Id { get; set; }
    [Reactive] public PivotItemModel SelectedPage { get; set; }
    [Reactive] public EpisodeModel SelectedEpisode { get; set; }
    [Reactive] public ObservableCollection<EpisodeModel> Episodes { get; set; }
    [Reactive] public List<TorrentModel> Torrents { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public AnimeModel Anime { get; set; }
    [ObservableAsProperty] public bool CanWatch { get; }
    [ObservableAsProperty] public bool HasTracking { get; }
    [ObservableAsProperty] public IList<AnimeSound> Sounds { get; }

    public string DefaultProviderType { get; }
    public ICommand PlaySound { get; }
    public ICommand Pause { get; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if(parameters.ContainsKey("Id"))
        {
            Id = (long)parameters.GetValueOrDefault("Id", (long)0);
        }
        else if(parameters.ContainsKey("Anime"))
        {
            Anime = (AnimeModel)parameters.GetValueOrDefault("Anime", null);
        }

        return Task.CompletedTask;
    }

}

public class PivotItemModel : ReactiveObject
{
    public string Header { get; set; }
    [Reactive] public bool Visible { get; set; } = true;
}

