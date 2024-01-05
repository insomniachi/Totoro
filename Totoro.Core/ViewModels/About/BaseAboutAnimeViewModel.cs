
namespace Totoro.Core.ViewModels.About;

public class BaseAboutAnimeViewModel : NavigatableViewModel
{
    [Reactive] public AnimeModel Anime { get; set; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if(parameters is not { Count : > 0})
        {
            return Task.CompletedTask;
        }

        Anime = (AnimeModel)parameters.GetValueOrDefault(nameof(Anime), null);
        return Task.CompletedTask;
    }
}
