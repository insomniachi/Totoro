using System.Diagnostics;
using AnimDL.Core;
using CommunityToolkit.WinUI.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Totoro.Core;
using Totoro.WinUI.Helpers;
using Totoro.WinUI.Models;
using Totoro.WinUI.Services;
using Windows.ApplicationModel;
using WinUIEx;

namespace Totoro.WinUI;

public partial class App : Application
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
                    .AddApplicationServices()
                    .AddAnimDL()
                    .AddMyAnimeList(context)
                    .AddTopLevelPages()
                    .AddDialogPages();

            services.AddSingleton(MessageBus.Current);
            services.AddTransient<DefaultExceptionHandler>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        })
        .Build();

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
        UnhandledException += App_UnhandledException;
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


    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) { }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        RxApp.DefaultExceptionHandler = GetService<DefaultExceptionHandler>();
        var activationService = GetService<IActivationService>();
        await activationService.ActivateAsync(args);
    }
}
