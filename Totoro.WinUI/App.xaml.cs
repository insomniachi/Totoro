using AnimDL.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Totoro.WinUI.Helpers;
using Totoro.WinUI.Models;
using Windows.ApplicationModel;

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

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        })
        .Build();

    public static T GetService<T>()
        where T : class
    {
        return _host.Services.GetService(typeof(T)) as T;
    }

    public static object GetService(System.Type t) => _host.Services.GetService(t);

    public static Window MainWindow { get; set; } = new Window() { Title = "AppDisplayName".GetLocalized() };

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }


    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) { }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        var activationService = GetService<IActivationService>();
        await activationService.ActivateAsync(args);
    }
}
