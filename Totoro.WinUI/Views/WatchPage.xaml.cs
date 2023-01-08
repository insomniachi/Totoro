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
