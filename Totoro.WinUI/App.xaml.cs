using System.Diagnostics;
using System.IO;
using System.Reactive.Concurrency;
using System.Text.Json;
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
using Windows.ApplicationModel.Activation;
using WinUIEx;
using Microsoft.Windows.AppNotifications;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Totoro.WinUI;

public partial class App : Application, IEnableLogger
{
    private static IList<IModule> _modules = 
        [
            new Plugins.Anime.AnimePahe.Module(),
		];

    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(config =>
        {
            config.AddJsonFile("appsettings.Development.json", true);
        })
        .ConfigureServices((context, services) =>
        {
            services.AddPlatformServices()
                    .AddTotoro()
                    .AddPluginRegistrar()
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

#if DEBUG
            foreach (var module in _modules)
            {
				module.RegisterServices(services);
			}
#endif

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
		AppNotificationManager.Default.NotificationInvoked += Default_NotificationInvoked;
        AppDomain.CurrentDomain.ProcessExit += OnExit;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppNotificationManager.Default.Register();
        UnhandledException += App_UnhandledException;
        DebugSettings.XamlResourceReferenceFailed += (_, e) => this.Log().Fatal(e.Message);
        DebugSettings.BindingFailed += (_, e) => this.Log().Warn(e.Message);
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Totoro", "Totoro.WinUI.exe");
        ActivationRegistrationManager.RegisterForProtocolActivation("totoro", "", "Totoro", exe);
    }

	private async void Default_NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
	{
		try
		{

			if (!args.Arguments.ContainsKey("Type"))
			{
				return;
			}

			switch (Enum.Parse<ToastType>(args.Arguments["Type"]))
			{
				case ToastType.DownloadComplete:
					Process.Start(new ProcessStartInfo { FileName = args.Arguments["File"], UseShellExecute = true });
					break;
				case ToastType.FinishedEpisode:
					var anime = JsonSerializer.Deserialize<AnimeModel>(args.Arguments["Payload"]);
					var ep = int.Parse(args.Arguments["Episode"]);
					var trackingService = GetService<ITrackingServiceContext>();
					await trackingService.Update(anime.Id, Tracking.WithEpisode(anime, ep));
					break;
				case ToastType.SelectAnime:
					var id = long.Parse(args.UserInput["animeId"]);
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
		AppNotificationManager.Default.Unregister();
	}


    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        RxApp.DefaultExceptionHandler.OnError(e.Exception);
        e.Handled = true;
    }

    protected async override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {

#if DEBUG
        var registrar = GetService<IPluginRegistrar>();
		foreach (var module in _modules)
		{
			module.OnServiceProviderCreated(_host.Services);
            module.RegisterPlugins(registrar);
		}
#endif

		StartFlyleaf();
        MainWindow = new MainWindow() { Title = "AppDisplayName".GetLocalized() };
        base.OnLaunched(args);
        RxApp.DefaultExceptionHandler = GetService<DefaultExceptionHandler>();
        Commands = GetService<TotoroCommands>();
        ConfigureLogging();

#if RELEASE
        await GetService<IPluginManager>().Initialize(GetService<IKnownFolders>().Plugins);
#endif
        await GetService<IActivationService>().ActivateAsync(args);

        var actArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        App_Activated(actArgs);
    }

    private static void StartFlyleaf()
    {
        FlyleafLib.Engine.Start(new FlyleafLib.EngineConfig()
        {
#if RELEASE
            FFmpegPath = @"FFmpeg",
            FFmpegLogLevel = Flyleaf.FFmpeg.LogLevel.Quiet,
            LogLevel = FlyleafLib.LogLevel.Quiet,

#else
			FFmpegLogLevel = Flyleaf.FFmpeg.LogLevel.Warn,
            LogLevel = FlyleafLib.LogLevel.Debug,
            LogOutput = ":debug",
            FFmpegPath = @"E:\FFmpeg",
#endif
            UIRefresh = false,    // Required for Activity, BufferedDuration, Stats in combination with Config.Player.Stats = true
            UIRefreshInterval = 250,      // How often (in ms) to notify the UI
            UICurTimePerSecond = true,     // Whether to notify UI for CurTime only when it's second changed or by UIRefreshInterval
        });
    }

    private static void ConfigureLogging()
    {
        var knownFolders = GetService<IKnownFolders>();
        var log = Path.Combine(knownFolders.Logs, "log.txt");
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
