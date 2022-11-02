using System.ComponentModel;
using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Totoro.Core.Services;
using Totoro.Core.Services.AnimixPlay;
using Totoro.Core.Services.MyAnimeList;
using Totoro.Core.Services.ShanaProject;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Activation;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.Dialogs.Views;
using Totoro.WinUI.Media;
using Totoro.WinUI.Services;
using Totoro.WinUI.ViewModels;
using Totoro.WinUI.Views;

namespace Totoro.WinUI.Helpers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPage<TViewModel, TView>(this IServiceCollection services)
        where TViewModel : class, INotifyPropertyChanged
        where TView : class, IViewFor<TViewModel>
    {
        services.AddTransient<TViewModel>();
        services.AddTransient<IViewFor<TViewModel>, TView>();
        return services;
    }

    public static IServiceCollection AddPageForNavigation<TViewModel, TView>(this IServiceCollection services)
        where TViewModel : class, INotifyPropertyChanged
        where TView : class, IViewFor<TViewModel>
    {
        services.AddPage<TViewModel, TView>();
        services.AddTransient<ViewType<TViewModel>>(x => new(typeof(TView)));
        services.AddTransient<ViewType>(x => x.GetService<ViewType<TViewModel>>());
        return services;
    }

    public static IServiceCollection AddCommonPages(this IServiceCollection services)
    {
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<ViewType<SettingsViewModel>>(x => new(typeof(SettingsPage)));
        services.AddTransient<ViewType>(x => x.GetService<ViewType<SettingsViewModel>>());
        services.AddTransient<ISettings>(x => x.GetRequiredService<SettingsViewModel>());
        services.AddTransient<ShellPage>();
        services.AddSingleton<ShellViewModel>();

        return services;
    }

    public static IServiceCollection AddMyAnimeList(this IServiceCollection services, HostBuilderContext context)
    {
        services.AddTransient<ITrackingService, MyAnimeListTrackingService>();
        services.AddTransient<IAnimeService, MyAnimeListService>();

        services.AddSingleton<IMalClient, MalClient>(x =>
        {
            var token = x.GetRequiredService<ILocalSettingsService>().ReadSetting<OAuthToken>("MalToken");
            var clientId = context.Configuration["ClientId"];
            if (token is { IsExpired: true })
            {
                token = MalAuthHelper.RefreshToken(token.RefreshToken, clientId).Result;
            }
            var client = new MalClient();
            if (token is not null && !string.IsNullOrEmpty(token.AccessToken))
            {
                client.SetAccessToken(token.AccessToken);
            }
            client.SetClientId(clientId);
            return client;
        });

        return services;
    }

    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        services.AddTransient<INavigationViewService, NavigationViewService>();
        services.AddSingleton<IWinUINavigationService, NavigationService>();
        services.AddSingleton<INavigationService>(x => x.GetRequiredService<IWinUINavigationService>());
        services.AddSingleton<IActivationService, ActivationService>();
        services.AddTransient<IContentDialogService, ContentDialogService>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();
        services.AddTransient<IViewService, ViewService>();
        services.AddSingleton<IPlaybackStateStorage, PlaybackStateStorage>();
        services.AddSingleton<IDiscordRichPresense, DiscordRichPresense>();
        services.AddTransient<MalToModelConverter>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IVolatileStateStorage, VolatileStateStorage>();
        services.AddSingleton<ISchedule, Schedule>();
        services.AddTransient<IRecentEpisodesProvider, AnimixPlayEpisodesProvider>();
        services.AddTransient<IFeaturedAnimeProvider, AnimixPlayFeaturedAnimeProvider>();
        services.AddTransient<IMediaPlayer, WinUIMediaPlayerWrapper>();
        services.AddSingleton<ITimestampsService, TimestampsService>();
        services.AddTransient<IAnimeIdService, AnimeIdService>();
        services.AddSingleton<IAnimeSoundsService, AnimeSoundsService>();
        services.AddMemoryCache();
        services.AddHttpClient();

        services.AddTransient<IShanaProjectService, ShanaProjectService>();

        return services;
    }

    public static IServiceCollection AddTopLevelPages(this IServiceCollection services)
    {
        services.AddCommonPages();
        services.AddPageForNavigation<UserListViewModel, UserListPage>();
        services.AddPageForNavigation<WatchViewModel, WatchPage>();
        services.AddPageForNavigation<SeasonalViewModel, SeasonalPage>();
        services.AddPageForNavigation<ScheduleViewModel, SchedulePage>();
        services.AddPageForNavigation<DiscoverViewModel, DiscoverPage>();
        services.AddPageForNavigation<AboutAnimeViewModel, AboutAnimePage>();

        return services;
    }

    public static IServiceCollection AddDialogPages(this IServiceCollection services)
    {
        services.AddPage<UpdateAnimeStatusViewModel, UpdateAnimeStatusView>();
        services.AddPage<ChooseSearchResultViewModel, ChooseSearchResultView>();
        services.AddPage<AuthenticateMyAnimeListViewModel, AuthenticateMyAnimeListView>();
        services.AddPage<PlayVideoDialogViewModel, VideoView>();
        return services;
    }
}
