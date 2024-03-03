using System.Data;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Media.Flyleaf;
using Totoro.WinUI.Media.Wmp;
using Totoro.WinUI.UserControls;

namespace Totoro.WinUI.Views;

public class WatchPageBase : ReactivePage<WatchViewModel> { }

public sealed partial class WatchPage : WatchPageBase
{
    private PipWindow _pipWindow;

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
                    //else if (wrapper is LibVLCMediaPlayerWrapper vlcWrapper)
                    //{
                    //    SubscribeDoubleTap(VlcMediaPlayerElement, windowService);
                    //    VlcMediaPlayerElement.MediaPlayer = vlcWrapper;
                    //}
                    else if(wrapper is FlyleafMediaPlayerWrapper flyleafWrapper)
                    {
                        SubscribeDoubleTap(FlyleafMediaPlayerElement, windowService);
                        flyleafWrapper.SetTransportControls(FlyleafMediaPlayerElement.TransportControls);
                        flyleafWrapper.WhenAnyValue(x => x.MediaPlayer)
                                      .ObserveOn(RxApp.MainThreadScheduler)
                                      .Subscribe(mp => FlyleafMediaPlayerElement.Player = mp);
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
                        .TransportControls
                        .OnPiPModeToggle
                        .Where(x => x)
                        .Subscribe(_ => EnterPiPMode());

                    //wrapper
                    //    .TransportControls
                    //    .PlaybackRateChanged
                    //    .Subscribe(rate => wrapper.SetPlaybackRate(rate));

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

            this.Events()
                .PointerMoved
                .Subscribe(_ => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow));

            windowService
            .IsFullWindowChanged
            .ObserveOn(RxApp.MainThreadScheduler)
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

    private void EnterPiPMode()
    {
        MainGrid.Children.Remove(MediaPlayer);
        _pipWindow = App.GetService<PipWindow>();
        _pipWindow.Initalize(MediaPlayer, GetWindowTitle());
        _pipWindow.Activate();
        _pipWindow.Closed += (_, _) =>
        {
            var grid = _pipWindow.WindowContent as Grid;
            grid.Children.Remove(MediaPlayer);
            MainGrid.Children.Add(MediaPlayer);
            ViewModel.MediaPlayer.TransportControls.TogglePiPMode();
        };
    }

    public static SolidColorBrush Foreground(bool isFiller)
    {
        return isFiller
            ? new SolidColorBrush(Colors.Yellow)
            : new SolidColorBrush(Colors.White);
    }

    private string GetWindowTitle()
    {
        if(ViewModel.Anime is null)
        {
            return ViewModel.EpisodeModels?.Current?.EpisodeTitle;
        }

        return $"{ViewModel.Anime.Title} - {ViewModel.EpisodeModels?.Current?.EpisodeNumber} - {ViewModel.EpisodeModels?.Current?.EpisodeTitle}";
    }

    private void KeyboardAccelerator_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        ProtectedCursor?.Dispose();
    }
}
