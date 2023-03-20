using ReactiveMarbles.ObservableEvents;
using Totoro.Core;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
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
            var windowService = App.GetService<IWindowService>();
            this.WhenAnyValue(x => x.ViewModel.MediaPlayer)
                .Select(mediaPlayer => mediaPlayer as WinUIMediaPlayerWrapper)
                .WhereNotNull()
                .Subscribe(wrapper =>
                {
                    MediaPlayerElement.SetMediaPlayer(wrapper.GetMediaPlayer());
                    var transportControls = wrapper.TransportControls as CustomMediaTransportControls;
                    this.WhenAnyValue(x => x.ViewModel.Qualities)
                        .Subscribe(qualities => transportControls.Qualities = qualities);

                    this.WhenAnyValue(x => x.ViewModel.SelectedQuality)
                        .Subscribe(quality => transportControls.SelectedQuality = quality);

                    transportControls.WhenAnyValue(x => x.SelectedQuality)
                        .Subscribe(quality => ViewModel.SelectedQuality = quality);
                })
                .DisposeWith(d);

            windowService
            .IsFullWindowChanged
            .Subscribe(isFullWindow => EpisodesExpander.Visibility = isFullWindow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible);

            MediaPlayerElement
            .Events()
            .DoubleTapped
            .Subscribe(_ => windowService.ToggleIsFullWindow())
            .DisposeWith(d);

            ViewModel
            .MediaPlayer
            .DurationChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Where(x => x > TimeSpan.Zero && ViewModel.AutoFullScreen)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Subscribe(x => windowService.SetIsFullWindow(true))
            .DisposeWith(d);

        });
    }
}
