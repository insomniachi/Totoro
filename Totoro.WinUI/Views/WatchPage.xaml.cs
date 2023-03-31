using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Media;
using Totoro.WinUI.Media.Vlc;
using Totoro.WinUI.Media.Wmp;

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
                .WhereNotNull()
                .Subscribe(mp =>
                {
                    if (mp is WinUIMediaPlayerWrapper wmpWrapper)
                    {
                        MediaPlayerElement.SetMediaPlayer(wmpWrapper.GetMediaPlayer());
                        MediaPlayerElement.Events()
                                          .DoubleTapped
                                          .Subscribe(_ => windowService.ToggleIsFullWindow())
                                          .DisposeWith(d);

                        var transportControls = wmpWrapper.TransportControls as CustomMediaTransportControls;
                        this.WhenAnyValue(x => x.ViewModel.Qualities)
                            .Subscribe(qualities => transportControls.Qualities = qualities);

                        this.WhenAnyValue(x => x.ViewModel.SelectedQuality)
                            .Subscribe(quality => transportControls.SelectedQuality = quality);

                        transportControls.WhenAnyValue(x => x.SelectedQuality)
                            .Subscribe(quality => ViewModel.SelectedQuality = quality);

                        ViewModel.SetMediaPlayer(ViewModel.MediaPlayer);
                        ViewModel.SubscribeTransportControlEvents();
                    }
                    else if (mp is LibVLCMediaPlayerWrapper vlcWrapper)
                    {
                        VlcMediaPlayerElement.Events()
                                             .DoubleTapped
                                             .Subscribe(_ => windowService.ToggleIsFullWindow())
                                             .DisposeWith(d);
                    }

                })
                .DisposeWith(d);

            windowService
            .IsFullWindowChanged
            .Subscribe(isFullWindow => EpisodesExpander.Visibility = isFullWindow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible);

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

    private void VlcMediaPlayerElement_Initialized(object sender, EventArgs e)
    {
        if (ViewModel.MediaPlayer is not LibVLCMediaPlayerWrapper wrapper)
        {
            return;
        }

        wrapper.Initialize(VlcMediaPlayerElement.LibVLC, VlcMediaPlayerElement.MediaPlayer);
        wrapper.SetTransportControls(VlcMediaPlayerElement.TransportControls);
        ViewModel.SetMediaPlayer(ViewModel.MediaPlayer);
        ViewModel.SubscribeTransportControlEvents();
    }
}
