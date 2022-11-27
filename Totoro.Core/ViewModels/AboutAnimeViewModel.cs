

namespace Totoro.Core.ViewModels;

public class AboutAnimeViewModel : NavigatableViewModel
{
	private readonly ObservableAsPropertyHelper<FullAnimeModel> _anime;
	private readonly ObservableAsPropertyHelper<bool> _hasTracking;
	private readonly ObservableAsPropertyHelper<IList<AnimeSound>> _sounds;

	public AboutAnimeViewModel(IAnimeService animeService,
							   INavigationService navigationService,
							   IViewService viewService,
							   IAnimeSoundsService animeSoundService)
	{
		WatchEpidoes = ReactiveCommand.Create(() => navigationService.NavigateTo<WatchViewModel>(parameter: new Dictionary<string, object>
		{
			["Anime"] = Anime
		}));

		UpdateStatus = ReactiveCommand.CreateFromTask<IAnimeModel>(viewService.UpdateAnimeStatus);
		PlaySound = ReactiveCommand.Create<AnimeSound>(sound => viewService.PlayVideo(sound.SongName, sound.Url));
		Pause = ReactiveCommand.Create(animeSoundService.Pause);

		this.ObservableForProperty(x => x.Id, x => x)
			.Where(id => id > 0)
			.SelectMany(animeService.GetInformation)
			.ToProperty(this, nameof(Anime), out _anime, scheduler: RxApp.MainThreadScheduler);

		this.WhenAnyValue(x => x.Anime)
			.WhereNotNull()
			.Select(anime => anime.Tracking is { })
			.ToProperty(this, nameof(HasTracking), out _hasTracking, scheduler: RxApp.MainThreadScheduler);

		this.ObservableForProperty(x => x.Id, x => x)
			.Where(id => id > 0)
			.Select(animeSoundService.GetThemes)
			.ToProperty(this, nameof(Sounds), out _sounds, scheduler: RxApp.MainThreadScheduler);
	}

    [Reactive] public long Id { get; set; }
	public FullAnimeModel Anime => _anime.Value;
	public bool HasTracking => _hasTracking.Value;
	public IList<AnimeSound> Sounds => _sounds.Value;
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
