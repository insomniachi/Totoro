using Totoro.Core.ViewModels;
using Totoro.WinUI.Media;
using ReactiveMarbles.ObservableEvents;

namespace Totoro.WinUI.Views;

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
                .DisposeWith(d);

            SearchBox
            .Events()
            .SuggestionChosen
            .Select(@event => @event.args.SelectedItem as SearchResultModel)
            .Do(result => ViewModel.Anime = result)
            .SelectMany(result => ViewModel.Find(result.Id, result.Title))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => ViewModel.SelectedAnimeResult = x)
            .DisposeWith(d);

            TransportControls
            .OnNextTrack
            .InvokeCommand(ViewModel.NextEpisode)
            .DisposeWith(d);

            TransportControls
            .OnPrevTrack
            .InvokeCommand(ViewModel.PrevEpisode);

            TransportControls
            .OnSkipIntro
            .InvokeCommand(ViewModel.SkipOpening);

            TransportControls
            .OnQualityChanged
            .InvokeCommand(ViewModel.ChangeQuality);

            TransportControls
            .OnDynamicSkipIntro
            .InvokeCommand(ViewModel.SkipOpeningDynamic);

        });
    }
}
