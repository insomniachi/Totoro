using System.Diagnostics;
using System.IO;
using System.Reactive.Concurrency;
using System.Text.Json;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using Splat;
using Splat.Serilog;
using Totoro.Core;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Manga;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Torrents.Contracts;
using Totoro.WinUI.Helpers;
using Totoro.WinUI.Models;
using Totoro.WinUI.Services;
using Totoro.WinUI.ViewModels;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using WinUIEx;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Totoro.WinUI;

public partial class App : Application, IEnableLogger
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(config =>
        {
            if (RuntimeHelper.IsMSIX)
            {
                config.SetBasePath(Package.Current.InstalledLocation.Path)
                      .AddJsonFile("appsettings.json");
            }

#if DEBUG
            config.AddJsonFile("appsettings.Development.json");
#endif
        })
        .ConfigureServices((context, services) =>
        {
            services.AddPlatformServices()
                    .AddTotoro()
                    .AddPlugins()
                    .AddTracking()
                    .AddTorrenting()
                    .AddMediaDetection()
                    .AddTopLevelPages()
                    .AddDialogPages();

            services.AddSingleton(MessageBus.Current);
            services.AddTransient<DefaultExceptionHandler>();
            services.AddSingleton<IPluginManager>(x => new PluginManager(PluginFactory<AnimeProvider>.Instance,
                                                                         PluginFactory<MangaProvider>.Instance,
                                                                         PluginFactory<ITorrentTracker>.Instance,
                                                                         PluginFactory<INativeMediaPlayer>.Instance));

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        })
        .Build();

    public static TotoroCommands Commands { get; private set; }

#if RELEASE
    public static bool HandleClosedEvents { get; set; } = true;
#else
    public static bool HandleClosedEvents { get; set; }
#endif

    public static T GetService<T>()
        where T : class
    {
        try
        {
            return _host.Services.GetService<T>();
        }
        catch (Exception ex)
        {
            LogHost.Default.Error(ex, ex.Message);
            throw;
        }
    }


    public static T GetService<T>(object key)
        where T : class
    {
        try
        {
            return _host.Services.GetKeyedService<T>(key);
        }
        catch (Exception ex)
        {
            LogHost.Default.Error(ex, ex.Message);
            throw;
        }
    }

    public static object GetService(Type t) => _host.Services.GetService(t);

    public static WindowEx MainWindow { get; set; }

    public App()
    {
        InitializeComponent();
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
        AppDomain.CurrentDomain.ProcessExit += OnExit;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        UnhandledException += App_UnhandledException;
        DebugSettings.XamlResourceReferenceFailed += (_, e) => this.Log().Fatal(e.Message);
        DebugSettings.BindingFailed += (_, e) => this.Log().Warn(e.Message);
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Totoro", "Totoro.WinUI.exe");
        ActivationRegistrationManager.RegisterForProtocolActivation("totoro", "", "Totoro", exe);
    }

    private void App_Activated(AppActivationArguments e)
    {
        this.Log().Info($"Activation Kind : {e.Kind}");
        if (e.Kind == ExtendedActivationKind.Protocol)
        {
            this.Log().Info("Protocol Activation");
            IProtocolActivatedEventArgs protocolArgs = e.Data as IProtocolActivatedEventArgs;
            this.Log().Info(protocolArgs.Uri.AbsolutePath);
        }
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        NativeMethods.AllowSleep();
        this.Log().Fatal(e.ExceptionObject);
    }

    private void OnExit(object sender, EventArgs e)
    {
        ToastNotificationManagerCompat.Uninstall();
    }

    private async void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        try
        {
            var args = ToastArguments.Parse(e.Argument);

            if (!args.Contains("Type"))
            {
                return;
            }

            switch (args.GetEnum<ToastType>("Type"))
            {
                case ToastType.DownloadComplete:
                    Process.Start(new ProcessStartInfo { FileName = args.Get("File"), UseShellExecute = true });
                    break;
                case ToastType.FinishedEpisode:
                    var anime = JsonSerializer.Deserialize<AnimeModel>(args.Get("Payload"));
                    var ep = args.GetInt("Episode");
                    var trackingService = GetService<ITrackingServiceContext>();
                    await trackingService.Update(anime.Id, Tracking.WithEpisode(anime, ep));
                    break;
                case ToastType.SelectAnime:
                    var id = long.Parse((string)e.UserInput["animeId"]);
                    var nowPlayingViewModel = GetService<NowPlayingViewModel>();
                    RxApp.MainThreadScheduler.Schedule(async () => await nowPlayingViewModel.SetAnime(id));
                    break;
            }
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
        }
    }


    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        NativeMethods.AllowSleep();
        this.Log().Fatal(e.Exception, e.Message);
    }

    protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow() { Title = "AppDisplayName".GetLocalized() };
        base.OnLaunched(args);
        RxApp.DefaultExceptionHandler = GetService<DefaultExceptionHandler>();
        Commands = GetService<TotoroCommands>();
        ConfigureLogging();

#if RELEASE
        await GetService<IPluginManager>().Initialize(GetService<IKnownFolders>().Plugins);
#endif
        await GetService<IActivationService>().ActivateAsync(args);

        var actArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        App_Activated(actArgs);
    }

    private static void ConfigureLogging()
    {
        var knownFolders = GetService<IKnownFolders>();
        var log = System.IO.Path.Combine(knownFolders.Logs, "log.txt");
        var mimimumLogLevel = GetService<ISettings>().MinimumLogLevel;
        var configuration = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.File(log, rollingInterval: RollingInterval.Day);

        switch (mimimumLogLevel)
        {
            case LogLevel.Debug:
                configuration.MinimumLevel.Debug();
                break;
            case LogLevel.Information:
                configuration.MinimumLevel.Information();
                break;
            case LogLevel.Warning:
                configuration.MinimumLevel.Warning();
                break;
            case LogLevel.Error:
                configuration.MinimumLevel.Error();
                break;
            case LogLevel.Critical:
                configuration.MinimumLevel.Fatal();
                break;
        }

        Log.Logger = configuration.CreateLogger();
        Locator.CurrentMutable.UseSerilogFullLogger();
    }
}
