using Totoro.Core.ViewModels;
using Totoro.WinUI.Media;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core;
using Windows.Storage.Pickers;

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

            TransportControls
            .OnAddCc
            .Subscribe(async _ =>
            {
                // Create a file picker
                var openPicker = new FileOpenPicker();

                // Retrieve the window handle (HWND) of the current WinUI 3 window.
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

                // Initialize the file picker with the window handle (HWND).
                WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

                // Set options for your file picker
                openPicker.ViewMode = PickerViewMode.Thumbnail;
                openPicker.FileTypeFilter.Add("*");

                ViewModel.MediaPlayer.Pause();

                // Open the picker for the user to pick a file
                var file = await openPicker.PickSingleFileAsync();

                if(file is not null)
                {
                    await ViewModel.MediaPlayer.SetSubtitleFromFile(file.Path);
                }

                ViewModel.MediaPlayer.Play();

            })
            .DisposeWith(d);
        });
    }
}
