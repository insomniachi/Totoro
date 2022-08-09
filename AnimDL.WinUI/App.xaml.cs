using AnimDL.Core;
using AnimDL.WinUI.Activation;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Contracts.Services;
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
using ReactiveUI;
using Windows.ApplicationModel;

// To learn more about WinUI3, see: https://docs.microsoft.com/windows/apps/winui/winui3/.
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

            // Other Activation Handlers

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<IContentDialogService, ContentDialogService>();
            services.AddTransient<IViewService, ViewService>();
            services.AddSingleton<IPlaybackStateStorage, PlaybackStateStorage>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IVolatileStateStorage, VolatileStateStorage>();
            services.AddAnimDL();

            // Views and ViewModels
            services.AddSingleton<SettingsViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<ISettings>(x => x.GetRequiredService<SettingsViewModel>());
            services.AddTransient<UserListViewModel>();
            services.AddTransient<UserListPage>();
            services.AddTransient<ShellPage>();
            services.AddSingleton<ShellViewModel>();
            services.AddTransient<WatchViewModel>();
            services.AddTransient<WatchPage>();
            services.AddTransient<SeasonalPage>();
            services.AddTransient<SeasonalViewModel>();
            services.AddTransient<SchedulePage>();
            services.AddTransient<ScheduleViewModel>();
            services.AddTransient<DiscoverPage>();
            services.AddTransient<DiscoverViewModel>();


            // Dialogs
            services.AddTransient<UpdateAnimeStatusViewModel>();
            services.AddTransient<IViewFor<UpdateAnimeStatusViewModel>, UpdateAnimeStatusView>();
            services.AddTransient<ChooseSearchResultViewModel>();
            services.AddTransient<IViewFor<ChooseSearchResultViewModel>, ChooseSearchResultView>();
            services.AddTransient<AuthenticateMyAnimeListViewModel>();
            services.AddTransient<IViewFor<AuthenticateMyAnimeListViewModel>, AuthenticateMyAnimeListView>();

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
