using System.ComponentModel;
using Microsoft.UI.Xaml;
using Totoro.Core;
using Totoro.Core.Services;
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

    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        services.AddTransient<INavigationViewService, NavigationViewService>();
        services.AddSingleton<IWinUINavigationService, NavigationService>();
        services.AddSingleton<INavigationService>(x => x.GetRequiredService<IWinUINavigationService>());
        services.AddSingleton<IActivationService, ActivationService>();
        services.AddTransient<IContentDialogService, ContentDialogService>();
        services.AddTransient<IToastService, ToastService>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IDiscordRichPresense, DiscordRichPresense>();
        services.AddSingleton<IPlaybackStateStorage, PlaybackStateStorage>();
        services.AddSingleton<IVolatileStateStorage, VolatileStateStorage>();
        services.AddSingleton<ISchedule, Schedule>();
        services.AddSingleton<ITimestampsService, TimestampsService>();
        services.AddSingleton<IAnimeSoundsService, AnimeSoundsService>();
        services.AddSingleton<ITorrentsService, TorrentsService>();
        services.AddSingleton<ILocalMediaService, LocalMediaService>();
        services.AddSingleton<IAiredEpisodeNotifier, AiredEpisodeNotifier>();
        services.AddSingleton<IUpdateService, WindowsUpdateService>();
        services.AddSingleton<ITrackingServiceContext, TrackingServiceContext>();
        services.AddSingleton<IAnimeServiceContext, AnimeServiceContext>();

        services.AddTransient<IFileService, FileService>();
        services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();
        services.AddTransient<IViewService, ViewService>();
        services.AddTransient<IMediaPlayer, WinUIMediaPlayerWrapper>();
        services.AddTransient<IAnimeIdService, AnimeIdService>();
        services.AddTransient<IShanaProjectService, ShanaProjectService>();
        services.AddTransient<TotoroCommands>();
        services.AddTransient<ISystemClock, SystemClock>();
        services.AddTransient<ISchedulerProvider, SchedulerProvider>();
        services.AddTransient<IStreamPageMapper, StreamPageMapper>();

        services.AddMemoryCache();
        services.AddHttpClient();

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
        services.AddPageForNavigation<DownloadViewModel, DownloadPage>();
        return services;
    }

    public static IServiceCollection AddDialogPages(this IServiceCollection services)
    {
        services.AddPage<UpdateAnimeStatusViewModel, UpdateAnimeStatusView>();
        services.AddPage<ChooseSearchResultViewModel, ChooseSearchResultView>();
        services.AddPage<AuthenticateMyAnimeListViewModel, AuthenticateMyAnimeListView>();
        services.AddPage<PlayVideoDialogViewModel, VideoView>();
        services.AddPage<SelectModelViewModel, SelectModelView>();
        services.AddPage<SubmitTimeStampsViewModel, SubmitTimeStampsView>();
        return services;
    }
}
