using System.Threading;
using FlyleafLib;
using FlyleafLib.MediaPlayer;
using Microsoft.UI.Xaml;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views
{
    public class BlankPageBase : ReactivePage<BlankPageViewModel> { }


    public sealed partial class BlankPage1 : BlankPageBase
    {
        public Player Player { get; set; }
        public Config Config { get; set; }

        public BlankPage1()
        {
            Engine.Start(new EngineConfig()
            {
                FFmpegPath = @"E:\FFmpeg",
                FFmpegDevices = false,    // Prevents loading avdevice/avfilter dll files. Enable it only if you plan to use dshow/gdigrab etc.

#if RELEASE
            FFmpegLogLevel      = FFmpegLogLevel.Quiet,
            LogLevel            = LogLevel.Quiet,

#else
                FFmpegLogLevel = FFmpegLogLevel.Warning,
                LogLevel = LogLevel.Debug,
                LogOutput = ":debug",
                //LogOutput         = ":console",
                //LogOutput         = @"C:\Flyleaf\Logs\flyleaf.log",                
#endif

                //PluginsPath       = @"C:\Flyleaf\Plugins",

                UIRefresh = false,    // Required for Activity, BufferedDuration, Stats in combination with Config.Player.Stats = true
                UIRefreshInterval = 250,      // How often (in ms) to notify the UI
                UICurTimePerSecond = true,     // Whether to notify UI for CurTime only when it's second changed or by UIRefreshInterval
            });

            this.InitializeComponent();

            Config = new Config();
            Config.Video.BackgroundColor = System.Windows.Media.Colors.DarkGray;

            Player = new Player(Config);
            Player.OpenAsync(@"http://mum1.download.real-debrid.com/d/NKBC4XRHRYNTA41/%5BErai-raws%5D%20Hyakkano%20-%2011%20%5B1080p%5D%5BMultiple%20Subtitle%5D%5BD55CE876%5D.mkv");

            InitializeComponent();
            rootGrid.DataContext = this;

            // Keyboard focus fix
            FSC.FullScreenEnter += (o, e) => flyleafHost.KFC.Focus(FocusState.Keyboard);
            FSC.FullScreenExit += (o, e) => Task.Run(() =>
            { Thread.Sleep(10); Utils.UIInvoke(() => flyleafHost.KFC.Focus(FocusState.Keyboard)); });

            Loaded += BlankPage1_Loaded;
        }

        private void BlankPage1_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}

namespace Totoro.Core.ViewModels
{
    public class BlankPageViewModel : NavigatableViewModel
    {

    }
}
