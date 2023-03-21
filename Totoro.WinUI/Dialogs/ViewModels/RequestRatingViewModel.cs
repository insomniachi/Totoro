namespace Totoro.WinUI.Dialogs.ViewModels;

public class RequestRatingViewModel : DialogViewModel
{
    [Reactive] public int Rating { get; set; }
    public IAnimeModel Anime { get; }
    
    public RequestRatingViewModel(IAnimeModel anime)
    {
        Anime = anime;
    }
}
