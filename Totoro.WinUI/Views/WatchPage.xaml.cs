using Totoro.Core.ViewModels;
using Totoro.WinUI.Media;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core;

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

            MediaPlayerElement
            .Events()
            .DoubleTapped
            .Subscribe(_ =>
            {
                MessageBus.Current.SendMessage(new RequestFullWindowMessage(!ViewModel.IsFullWindow));
                TransportControls.UpdateFullWindow(ViewModel.IsFullWindow);
            })
            .DisposeWith(d);

            ViewModel
            .MediaPlayer
            .DurationChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(x => x > TimeSpan.Zero && !ViewModel.IsFullWindow && ViewModel.AutoFullScreen)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Subscribe(x =>
            {
                MessageBus.Current.SendMessage(new RequestFullWindowMessage(true));
                TransportControls.UpdateFullWindow(true);
            })
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
            .OnDynamicSkip
            .InvokeCommand(ViewModel.SkipDynamic)
            .DisposeWith(d);

            TransportControls
            .OnSubmitTimeStamp
            .InvokeCommand(ViewModel.SubmitTimeStamp)
            .DisposeWith(d);
        });
    }
}
