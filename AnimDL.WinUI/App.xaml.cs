using AnimDL.Core;
using AnimDL.WinUI.Activation;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.Core;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Core.Contracts.Services;
using AnimDL.WinUI.Core.Services;
using AnimDL.WinUI.Dialogs.ViewModels;
using AnimDL.WinUI.Dialogs.Views;
using AnimDL.WinUI.Helpers;
using AnimDL.WinUI.Models;
using AnimDL.WinUI.Services;
using AnimDL.WinUI.ViewModels;
using AnimDL.WinUI.Views;
using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;

namespace AnimDL.WinUI;

public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(config => 
        {
            if(RuntimeHelper.IsMSIX)
            {
                config.SetBasePath(Package.Current.InstalledLocation.Path)
                      .AddJsonFile("appsettings.json");
            }
        })
        .ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddTransient<IContentDialogService, ContentDialogService>();
            services.AddTransient<IViewService, ViewService>();
            services.AddSingleton<IPlaybackStateStorage, PlaybackStateStorage>();
            services.AddSingleton<IDiscordRichPresense, DiscordRichPresense>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IVolatileStateStorage, VolatileStateStorage>();
            services.AddSingleton<ISchedule, Schedule>();
            services.AddAnimDL();

            services.AddCommonPages();
            
            // Navigatable views
            services.AddPageForNavigation<UserListViewModel, UserListPage>();
            services.AddPageForNavigation<WatchViewModel, WatchPage>();
            services.AddPageForNavigation<SeasonalViewModel, SeasonalPage>();
            services.AddPageForNavigation<ScheduleViewModel, SchedulePage>();
            services.AddPageForNavigation<DiscoverViewModel, DiscoverPage>();

            // Dialogs
            services.AddPage<UpdateAnimeStatusViewModel, UpdateAnimeStatusView>();
            services.AddPage<ChooseSearchResultViewModel, ChooseSearchResultView>();
            services.AddPage<AuthenticateMyAnimeListViewModel, AuthenticateMyAnimeListView>();

            services.AddSingleton<IMalClient, MalClient>(x => 
            {
                var token = x.GetRequiredService<ILocalSettingsService>().ReadSetting<OAuthToken>("MalToken");
                var clientId = context.Configuration["ClientId"];
                if (token is { IsExpired : true })
                {
                    token = MalAuthHelper.RefreshToken(token.RefreshToken, clientId).Result;
                }
                var client = new MalClient();
                if(token is not null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    client.SetAccessToken(token.AccessToken);
                }
                client.SetClientId(clientId);
                return client;
            });

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


    private void App_UnhandledException(object sender, UnhandledExceptionEventArgs e) { }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        var activationService = GetService<IActivationService>();
        await activationService.ActivateAsync(args);
    }
}
