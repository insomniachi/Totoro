

namespace Totoro.Core.ViewModels;

public class AboutAnimeViewModel : NavigatableViewModel
{
	private readonly ObservableAsPropertyHelper<FullAnimeModel> _anime;

	public AboutAnimeViewModel(IAnimeService animeService)
	{
		this.ObservableForProperty(x => x.Id, x => x)
			.Where(id => id > 0)
			.SelectMany(animeService.GetInformation)
			.ToProperty(this, nameof(Anime), out _anime, scheduler: RxApp.MainThreadScheduler);
	}

    [Reactive] public long Id { get; set; }
	public FullAnimeModel Anime => _anime.Value;

	public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
	{
		Id = (long)parameters.GetValueOrDefault("Id", (long)0);

		return Task.CompletedTask;
    }

}
