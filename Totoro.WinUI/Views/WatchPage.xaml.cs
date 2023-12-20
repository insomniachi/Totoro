using System.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Media;
using Totoro.WinUI.Media.Flyleaf;
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
            var viewService = App.GetService<IViewService>();
            var localSettingsService = App.GetService<ILocalSettingsService>();
            this.WhenAnyValue(x => x.ViewModel.MediaPlayer)
                .WhereNotNull()
                .Subscribe(wrapper =>
                {
                    var globalVolume = localSettingsService.ReadSetting<double>("Volume", 100);

                    if (wrapper is WinUIMediaPlayerWrapper wmpWrapper)
                    {
                        wmpWrapper.SetVolume(globalVolume);
                        wmpWrapper.VolumeChanged.Subscribe(volume => localSettingsService.SaveSetting("Volume", volume));
                        SubscribeDoubleTap(MediaPlayerElement, windowService);
                        MediaPlayerElement.SetMediaPlayer(wmpWrapper.GetMediaPlayer());
                        MediaPlayerElement.TransportControls = wmpWrapper.TransportControls as CustomMediaTransportControls;
                    }
                    else if (wrapper is LibVLCMediaPlayerWrapper vlcWrapper)
                    {
                        SubscribeDoubleTap(VlcMediaPlayerElement, windowService);
                        VlcMediaPlayerElement.MediaPlayer = vlcWrapper;
                    }
                    else if(wrapper is FlyleafMediaPlayerWrapper flyleafWrapper)
                    {
                        SubscribeDoubleTap(FlyleafMediaPlayerElement, windowService);
                        flyleafWrapper.SetTransportControls(FlyleafMediaPlayerElement.TransportControls);
                        FlyleafMediaPlayerElement.Player = flyleafWrapper.MediaPlayer;
                    }

                    this.WhenAnyValue(x => x.ViewModel.Qualities)
                        .Subscribe(resolutions => wrapper.TransportControls.Resolutions = resolutions);

                    this.WhenAnyValue(x => x.ViewModel.SelectedQuality)
                        .Subscribe(resolution => wrapper.TransportControls.SelectedResolution = resolution);

                    wrapper
                        .Playing
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(_ => VisualStateManager.GoToState((Control)wrapper.TransportControls, "ControlPanelFadeOut", true));

                    wrapper
                        .TransportControls
                        .WhenAnyValue(x => x.SelectedResolution)
                        .Subscribe(resolution => ViewModel.SelectedQuality = resolution);

                    wrapper
                        .TransportControls
                        .OnAddCc
                        .Subscribe(async _ =>
                        {
                            var subtitleFile = await viewService.BrowseSubtitle();
                            if (string.IsNullOrEmpty(subtitleFile))
                            {
                                return;
                            }
                            await wrapper.AddSubtitle(subtitleFile);
                        });

                    wrapper
                        .DurationChanged
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Where(x => x > TimeSpan.Zero && ViewModel.AutoFullScreen)
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .Subscribe(x => windowService.SetIsFullWindow(true))
                        .DisposeWith(d);

                    ViewModel.SetMediaPlayer(wrapper);
                    ViewModel.SubscribeTransportControlEvents();
                })
                .DisposeWith(d);

            windowService
            .IsFullWindowChanged
            .Subscribe(isFullWindow => EpisodesExpander.Visibility = isFullWindow ? Visibility.Collapsed : Visibility.Visible);

        });
    }

    private static void SubscribeDoubleTap(FrameworkElement mediaPlayerElement, IWindowService windowService)
    {
        mediaPlayerElement
            .Events()
            .DoubleTapped
            .Subscribe(_ => windowService.ToggleIsFullWindow());
    }


}
