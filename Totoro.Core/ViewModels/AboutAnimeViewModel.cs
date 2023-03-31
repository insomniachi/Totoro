namespace Totoro.Core.ViewModels;

public class AboutAnimeViewModel : NavigatableViewModel
{
    public ObservableCollection<PivotItemModel> Pages { get; } = new()
    {
        new PivotItemModel { Header = "Previews" },
        new PivotItemModel { Header = "Related" },
        new PivotItemModel { Header = "Recommended" },
        new PivotItemModel { Header = "OST"}
    };

    public AboutAnimeViewModel(IAnimeServiceContext animeService,
                               INavigationService navigationService,
                               IViewService viewService,
                               IAnimeSoundsService animeSoundService)
    {
        WatchEpidoes = ReactiveCommand.Create(() => navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>
        {
            ["Anime"] = Anime
        }));

        UpdateStatus = ReactiveCommand.CreateFromTask<IAnimeModel>(viewService.UpdateTracking);
        PlaySound = ReactiveCommand.Create<AnimeSound>(sound => viewService.PlayVideo(sound.SongName, sound.Url));
        Pause = ReactiveCommand.Create(animeSoundService.Pause);

        this.ObservableForProperty(x => x.Id, x => x)
            .Where(id => id > 0)
            .SelectMany(animeService.GetInformation)
            .ToPropertyEx(this, x => x.Anime, scheduler: RxApp.MainThreadScheduler);

        this.WhenAnyValue(x => x.Anime)
            .WhereNotNull()
            .Select(anime => anime.Tracking is { })
            .ToPropertyEx(this, x => x.HasTracking, scheduler: RxApp.MainThreadScheduler);

        this.ObservableForProperty(x => x.Id, x => x)
            .Where(id => id > 0)
            .Select(animeSoundService.GetThemes)
            .ToPropertyEx(this, x => x.Sounds, scheduler: RxApp.MainThreadScheduler);

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
            .Subscribe(anime =>
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
            });

        this.WhenAnyValue(x => x.SelectedPage)
            .Where(x => x is null && Pages.Any(x => x.Visible))
            .Subscribe(_ => SelectedPage = Pages.First(x => x.Visible));
    }

    [Reactive] public long Id { get; set; }
    [Reactive] public PivotItemModel SelectedPage { get; set; }
    [ObservableAsProperty] public AnimeModel Anime { get; }
    [ObservableAsProperty] public bool HasTracking { get; }
    [ObservableAsProperty] public IList<AnimeSound> Sounds { get; }
    public ICommand WatchEpidoes { get; }
    public ICommand UpdateStatus { get; }
    public ICommand PlaySound { get; }
    public ICommand Pause { get; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        Id = (long)parameters.GetValueOrDefault("Id", (long)0);
        return Task.CompletedTask;
    }

}

public class PivotItemModel : ReactiveObject
{
    public string Header { get; set; }
    [Reactive] public bool Visible { get; set; } = true;
}

