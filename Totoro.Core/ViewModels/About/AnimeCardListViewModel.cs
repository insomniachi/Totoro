
namespace Totoro.Core.ViewModels.About;

public class AnimeCardListViewModel : NavigatableViewModel 
{
    [Reactive] public List<AnimeModel> Anime { get; set; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        Anime = (List<AnimeModel>)parameters.GetValueOrDefault(nameof(Anime), new List<AnimeModel>());
        return Task.CompletedTask;
    }
}
