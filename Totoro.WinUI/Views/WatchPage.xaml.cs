using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Media;
using ReactiveMarbles.ObservableEvents;

namespace Totoro.WinUI.Views;

public class WatchPageBase : ReactivePage<WatchViewModel> { }

public sealed partial class WatchPage : WatchPageBase
{
    private string[] _swapChainOptions;
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
                    }
                    else if (mp is LibVLCMediaPlayerWrapper { IsInitialized: false } vlcWrapper && _swapChainOptions is not null)
                    {
                        vlcWrapper.Initialize(_swapChainOptions);
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

    private void VideoView_Initialized(object sender, LibVLCSharp.Platforms.Windows.InitializedEventArgs e)
    {
        if(ViewModel.MediaPlayer is not LibVLCMediaPlayerWrapper { IsInitialized: false } wrapper)
        {
            return;
        }

        _swapChainOptions = e.SwapChainOptions;
        wrapper.Initialize(e.SwapChainOptions);
    }
}
