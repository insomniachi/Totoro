﻿using System.Data;
using Microsoft.UI.Xaml;
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
                .Subscribe(wrapper =>
                {
                    if (wrapper is WinUIMediaPlayerWrapper wmpWrapper)
                    {
                        SubscribeDoubleTap(MediaPlayerElement, windowService);
                        MediaPlayerElement.SetMediaPlayer(wmpWrapper.GetMediaPlayer());
                        MediaPlayerElement.TransportControls = wmpWrapper.TransportControls as CustomMediaTransportControls;
                    }
                    else if (wrapper is LibVLCMediaPlayerWrapper vlcWrapper)
                    {
                        SubscribeDoubleTap(VlcMediaPlayerElement, windowService);
                        VlcMediaPlayerElement.MediaPlayer = vlcWrapper;
                    }

                    this.WhenAnyValue(x => x.ViewModel.Qualities)
                        .Subscribe(resolutions => wrapper.TransportControls.Resolutions = resolutions);

                    this.WhenAnyValue(x => x.ViewModel.SelectedQuality)
                        .Subscribe(resolution => wrapper.TransportControls.SelectedResolution = resolution);

                    wrapper
                        .TransportControls
                        .WhenAnyValue(x => x.SelectedResolution)
                        .Subscribe(resolution => ViewModel.SelectedQuality = resolution);

                    ViewModel.SetMediaPlayer(wrapper);
                    ViewModel.SubscribeTransportControlEvents();
                })
                .DisposeWith(d);

            windowService
            .IsFullWindowChanged
            .Subscribe(isFullWindow => EpisodesExpander.Visibility = isFullWindow ? Visibility.Collapsed : Visibility.Visible);

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

    private static void SubscribeDoubleTap(FrameworkElement mediaPlayerElement, IWindowService windowService)
    {
        mediaPlayerElement
            .Events()
            .DoubleTapped
            .Subscribe(_ => windowService.ToggleIsFullWindow());
    }
}
