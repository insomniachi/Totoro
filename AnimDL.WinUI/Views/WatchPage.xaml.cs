using AnimDL.UI.Core.ViewModels;
using ReactiveMarbles.ObservableEvents;

namespace AnimDL.WinUI.Views;

public class WatchPageBase : ReactivePage<WatchViewModel> { }

public sealed partial class WatchPage : WatchPageBase
{
    public WatchPage()
    {
        InitializeComponent();
       
        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.MediaPlayer)
                .Where(mediaPlayer => mediaPlayer is WinUIMediaPlayerWrapper)
                .Select(mediaPlayer => mediaPlayer as WinUIMediaPlayerWrapper)
                .Do(wrapper => MediaPlayerElement.SetMediaPlayer(wrapper.GetMediaPlayer()))
                .Subscribe()
                .DisposeWith(ViewModel.Garbage);

            SearchBox
            .Events()
            .SuggestionChosen
            .Select(@event => @event.args.SelectedItem as SearchResultModel)
            .Do(result => ViewModel.Anime = result)
            .SelectMany(result => ViewModel.Find(result.Id, result.Title))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => ViewModel.SelectedAnimeResult = x);
        });
    }
}
