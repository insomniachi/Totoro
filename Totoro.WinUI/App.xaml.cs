using System.Diagnostics;
using System.Net.Http;
using AnimDL.Core;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Serilog;
using Splat;
using Splat.Serilog;
using Totoro.Core;
using Totoro.WinUI.Helpers;
using Totoro.WinUI.Models;
using Totoro.WinUI.Services;
using Windows.ApplicationModel;
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
        })
        .ConfigureServices((context, services) =>
        {
            MessageBus.Current.RegisterMessageSource(Observable.Timer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)).Select(_ => new MinuteTick()));

            services.AddPlatformServices()
                    .AddTotoro()
                    .AddAnimDL()
                    .AddMyAnimeList()
                    .AddAniList()
                    .AddTopLevelPages()
                    .AddDialogPages();

            services.AddSingleton(MessageBus.Current);
            services.AddTransient<DefaultExceptionHandler>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        })
        .Build();

    public static TotoroCommands Commands { get; private set; }

    public static T GetService<T>()
        where T : class
    {
        return _host.Services.GetService(typeof(T)) as T;
    }

    public static object GetService(Type t) => _host.Services.GetService(t);

    public static WindowEx MainWindow { get; set; } = new MainWindow() { Title = "AppDisplayName".GetLocalized() };

    public App()
    {
        InitializeComponent();
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
        AppDomain.CurrentDomain.ProcessExit += OnExit;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        UnhandledException += App_UnhandledException;
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        this.Log().Fatal(e.ExceptionObject);
    }

    private void OnExit(object sender, EventArgs e)
    {
        ToastNotificationManagerCompat.Uninstall();
    }

    private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);

        switch (args.GetEnum<ToastType>("Type"))
        {
            case ToastType.DownloadComplete:
                Process.Start(new ProcessStartInfo { FileName = args.Get("File"), UseShellExecute = true });
                break;
        }

        if (args.GetBool("NeedUI"))
        {
            MainWindow.Activate();
        }
    }


    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        this.Log().Fatal(e.Exception, e.Message);
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        RxApp.DefaultExceptionHandler = GetService<DefaultExceptionHandler>();
        Commands = GetService<TotoroCommands>();
        ConfigureLogging();
        await GetService<ISettings>().UpdateUrls();
        var activationService = GetService<IActivationService>();
        await activationService.ActivateAsync(args);
    }

    private static void ConfigureLogging()
    {
        var appDataFolder = GetService<IOptions<LocalSettingsOptions>>().Value.ApplicationDataFolder;
        var log = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appDataFolder, "Logs/log.txt");
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
