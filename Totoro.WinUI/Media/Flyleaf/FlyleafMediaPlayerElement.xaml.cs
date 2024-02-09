using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using FlyleafLib;
using FlyleafLib.MediaPlayer;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.Contracts;


namespace Totoro.WinUI.Media.Flyleaf;

public sealed partial class FlyleafMediaPlayerElement : UserControl
{
    private readonly Subject<Unit> _pointerMoved = new();
    private readonly IWindowService _windowService = App.GetService<IWindowService>();

    public Player Player
    {
        get { return (Player)GetValue(PlayerProperty); }
        set { SetValue(PlayerProperty, value); }
    }

    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register("Player", typeof(Player), typeof(FlyleafMediaPlayerElement), new PropertyMetadata(null));

    public FlyleafMediaPlayerElement()
    {
        InitializeComponent();

        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);

        Engine.Start(new EngineConfig()
        {
            FFmpegDevices = false,    // Prevents loading avdevice/avfilter dll files. Enable it only if you plan to use dshow/gdigrab etc.

#if RELEASE
            FFmpegPath = @"FFmpeg",
            FFmpegLogLevel = FFmpegLogLevel.Quiet,
            LogLevel = LogLevel.Quiet,

#else
            FFmpegLogLevel = FFmpegLogLevel.Warning,
            LogLevel = LogLevel.Debug,
            LogOutput = ":debug",
            FFmpegPath = @"E:\FFmpeg",
            //LogOutput         = ":console",
            //LogOutput         = @"C:\Flyleaf\Logs\flyleaf.log",                
#endif

            //PluginsPath       = @"C:\Flyleaf\Plugins",

            UIRefresh = false,    // Required for Activity, BufferedDuration, Stats in combination with Config.Player.Stats = true
            UIRefreshInterval = 250,      // How often (in ms) to notify the UI
            UICurTimePerSecond = true,     // Whether to notify UI for CurTime only when it's second changed or by UIRefreshInterval
        });

        _pointerMoved
            .Throttle(TimeSpan.FromSeconds(3))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                TransportControls.Bar.Visibility = Visibility.Collapsed;

                if(_windowService.IsFullWindow)
                {
                    ProtectedCursor.Dispose();
                }

            }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.Player.Status)
            .Where(status => status is Status.Playing)
            .Subscribe(_ => ShowTransportControls());

        TransportControls.DoubleTapped += (sender, args) => args.Handled = true;
    }

    private void FSC_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        ShowTransportControls();
    }

    private void ShowTransportControls()
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            TransportControls.Bar.Visibility = Visibility.Visible;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow); 
        });

        _pointerMoved.OnNext(Unit.Default);
    }
}
