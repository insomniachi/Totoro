using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Media;

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
            .Where(_ => ViewModel.Anime is not null)
            .SelectMany(_ => ViewModel.UpdateTracking())
            .ObserveOn(RxApp.MainThreadScheduler)
            .InvokeCommand(ViewModel.NextEpisode)
            .DisposeWith(d);

            TransportControls
            .OnPrevTrack
            .InvokeCommand(ViewModel.PrevEpisode)
            .DisposeWith(d);

            TransportControls
            .OnSkipIntro
            .InvokeCommand(ViewModel.SkipOpening)
            .DisposeWith(d);

            TransportControls
            .OnQualityChanged
            .InvokeCommand(ViewModel.ChangeQuality)
            .DisposeWith(d);

            TransportControls
            .OnDynamicSkipIntro
            .InvokeCommand(ViewModel.SkipOpeningDynamic)
            .DisposeWith(d);
        });
    }
}
