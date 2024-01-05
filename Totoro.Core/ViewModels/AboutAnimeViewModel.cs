using System.Reactive.Concurrency;
using Totoro.Core.ViewModels.About;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.Core.ViewModels;

public class AboutAnimeViewModel : NavigatableViewModel
{
    private readonly SourceList<PivotItemModel> _sectionsList = new();
    private readonly ReadOnlyObservableCollection<PivotItemModel> _sections;

    public ObservableCollection<PivotItemModel> Pages { get; }

    public AboutAnimeViewModel(IAnimeServiceContext animeService,
                               IViewService viewService,
                               IAnimeSoundsService animeSoundService,
                               IPluginFactory<ITorrentTracker> torrentCatalogFactory,
                               ISettings settings,
                               IMyAnimeListService myAnimeListService,
                               IAnimeIdService animeIdService,
                               IDebridServiceContext debridServiceContext,
                               ISimklService simklService)
    {
        _sectionsList
            .Connect()
            .RefCount()
            .AutoRefresh(x => x.Visible)
            .Filter(x => x.Visible)
            .Bind(out _sections)
            .Subscribe()
            .DisposeWith(Garbage);

        _sectionsList.Add(new PivotItemModel
        {
            Header = "Previews",
            ViewModel = typeof(PreviewsViewModel)
        });
        _sectionsList.Add(new PivotItemModel
        {
            Header = "Related",
            ViewModel = typeof(AnimeCardListViewModel),
        });
        _sectionsList.Add(new PivotItemModel
        {
            Header = "Related",
            ViewModel = typeof(AnimeCardListViewModel),
        });
        _sectionsList.Add(new PivotItemModel
        {
            Header = "Previews",
            ViewModel = typeof(OriginalSoundTracksViewModel),
        });
        _sectionsList.Add(new PivotItemModel
        {
            Header = "Torrents",
            ViewModel = typeof(AnimeEpisodesTorrentViewModel)
        });

        ListType = settings.DefaultListService;
        if (PluginFactory<AnimeProvider>.Instance.Plugins.FirstOrDefault(x => x.Name == settings.DefaultProviderType) is { } provider)
        {
            DefaultProviderType = $"({provider.DisplayName})";
        }

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(anime =>
            {
                var previewsItem = _sectionsList.Items.ElementAt(0);
                var relatedItem = _sectionsList.Items.ElementAt(1);
                var recommendedItem = _sectionsList.Items.ElementAt(2);
                var ostsItem = _sectionsList.Items.ElementAt(3);
                var torrentsItem = _sectionsList.Items.ElementAt(4);
                var sounds = animeSoundService.GetThemes(anime.Id);

                previewsItem.NavigationParameters = new()
                {
                    [nameof(PreviewsViewModel.Anime)] = anime
                };
                relatedItem.NavigationParameters = new() 
                {
                    [nameof(AnimeCardListViewModel.Anime)] = anime.Related.ToList() 
                };
                recommendedItem.NavigationParameters = new() 
                {
                    [nameof(AnimeCardListViewModel.Anime)] = anime.Recommended.ToList()
                };
                ostsItem.NavigationParameters = new() 
                {
                    [nameof(OriginalSoundTracksViewModel.Sounds)] = sounds 
                };
                torrentsItem.NavigationParameters = new()
                {
                    [nameof(AnimeEpisodesTorrentViewModel.Anime)] = anime
                };

                if (anime.Videos is not { Count: > 0})
                {
                    previewsItem.Visible = false;
                }
                if(anime.Related is not { Length : > 0})
                {
                    relatedItem.Visible = false;
                }
                if (anime.Recommended is not { Length: > 0 })
                {
                    recommendedItem.Visible = false;
                }
                if (sounds is not { Count: > 0 })
                {
                    ostsItem.Visible = false;
                }

                SelectedSection = null;
                SelectedSection = Sections.FirstOrDefault();
            });

        this.ObservableForProperty(x => x.Id, x => x)
            .Where(id => id > 0)
            .Do(_ => RxApp.MainThreadScheduler.Schedule(() => IsLoading = true))
            .SelectMany(animeService.GetInformation)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                Anime = x;
                IsLoading = false;
            }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Select(anime => anime.Tracking is { })
            .ToPropertyEx(this, x => x.HasTracking, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async anime =>
            {
                var malId = anime.MalId ?? (await animeIdService.GetId(anime.Id)).MyAnimeList;
                if (malId is null)
                {
                    return;
                }

                ObservableCollection<EpisodeModel> episodes = new(await simklService.GetEpisodes(malId.Value));
                var lastEpisodeNumber = episodes.LastOrDefault()?.EpisodeNumber ?? 0;
                var count = anime.AiredEpisodes - lastEpisodeNumber;
                if (count > 0)
                {
                    foreach (var ep in Enumerable.Range(lastEpisodeNumber + 1, count))
                    {
                        episodes.Add(new EpisodeModel { EpisodeNumber = ep });
                    }
                }

                Episodes = episodes;
            });

        this.WhenAnyValue(x => x.Episodes, x => x.Anime)
            .Where(x => x is not (_, null))
            .Select(x => x.Item1 is { Count: > 0 } || x.Item2.AiringStatus is not AiringStatus.NotYetAired)
            .ToPropertyEx(this, x => x.CanWatch, scheduler: RxApp.MainThreadScheduler);
    }

    [Reactive] public ObservableCollection<EpisodeModel> Episodes { get; set; }
    [Reactive] public PivotItemModel SelectedSection { get; set; }
    [Reactive] public long Id { get; set; }
    [Reactive] public AnimeModel Anime { get; set; }
    [Reactive] public bool IsLoading { get; set; }
    [ObservableAsProperty] public bool CanWatch { get; }
    [ObservableAsProperty] public bool HasTracking { get; }

    public ReadOnlyObservableCollection<PivotItemModel> Sections => _sections;
    public string DefaultProviderType { get; }
    public ListServiceType ListType { get; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters.ContainsKey("Id"))
        {
            Id = (long)parameters.GetValueOrDefault("Id", (long)0);
        }
        else if (parameters.ContainsKey("Anime"))
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
    public Type ViewModel { get; set; }
    public Dictionary<string, object> NavigationParameters { get; set; } = [];
}

