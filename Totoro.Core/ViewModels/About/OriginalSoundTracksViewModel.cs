
namespace Totoro.Core.ViewModels.About;

public class OriginalSoundTracksViewModel : NavigatableViewModel 
{
    [Reactive] public IList<AnimeSound> Sounds { get; set; }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        Sounds = (IList<AnimeSound>)parameters.GetValueOrDefault(nameof(Sounds), new List<AnimeSound>());

        return Task.CompletedTask;
    }
}
