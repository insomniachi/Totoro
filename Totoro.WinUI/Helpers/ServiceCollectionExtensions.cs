using System.ComponentModel;
using Microsoft.UI.Xaml;
using Totoro.Core.ViewModels;
using Totoro.Core.ViewModels.About;
using Totoro.Core.ViewModels.Discover;
using Totoro.Core.ViewModels.Torrenting;
using Totoro.Plugins;
using Totoro.Plugins.MediaDetection;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.WinUI.Activation;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.Dialogs.Views;
using Totoro.WinUI.Media;
using Totoro.WinUI.Media.Flyleaf;
using Totoro.WinUI.Media.Wmp;
using Totoro.WinUI.Services;
using Totoro.WinUI.UserControls;
using Totoro.WinUI.ViewModels;
using Totoro.WinUI.Views;
using Totoro.WinUI.Views.AboutSections;
using Totoro.WinUI.Views.DiscoverSections;
using Totoro.WinUI.Views.TorrentingSections;

namespace Totoro.WinUI.Helpers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPage<TViewModel, TView>(this IServiceCollection services, bool isSingleton = false)
        where TViewModel : class, INotifyPropertyChanged
        where TView : class, IViewFor<TViewModel>
    {
        if (isSingleton)
        {
            services.AddSingleton<TViewModel>();
        }
        else
        {
            services.AddTransient<TViewModel>();
        }

        services.AddTransient<IViewFor<TViewModel>, TView>();
        return services;
    }

    public static IServiceCollection AddPageForNavigation<TViewModel, TView>(this IServiceCollection services, bool isSingleton = false)
        where TViewModel : class, INotifyPropertyChanged
        where TView : class, IViewFor<TViewModel>
    {
        services.AddPage<TViewModel, TView>(isSingleton);
        services.AddTransient<ViewType<TViewModel>>(x => new(typeof(TView)));
        services.AddTransient<ViewType>(x => x.GetService<ViewType<TViewModel>>());
        return services;
    }

    public static IServiceCollection AddCommonPages(this IServiceCollection services)
    {
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<ViewType<SettingsViewModel>>(x => new(typeof(SettingsPage)));
        services.AddTransient<ViewType>(x => x.GetService<ViewType<SettingsViewModel>>());
        services.AddTransient<ShellPage>();
        services.AddSingleton<ShellViewModel>();

        return services;
    }

    public static IServiceCollection AddMediaDetection(this IServiceCollection services)
    {
        services.AddSingleton<ProcessWatcher>();
        services.AddTransient<NativeMediaPlayerTrackingUpdater>();
        services.AddTransient<NativeMediaPlayerDiscordRichPresenseUpdater>();
        services.AddSingleton<ExternalMediaPlayerLauncher>();

#if DEBUG
        PluginFactory<INativeMediaPlayer>.Instance.LoadPlugin(new Plugins.MediaDetection.Vlc.Plugin());
        PluginFactory<INativeMediaPlayer>.Instance.LoadPlugin(new Plugins.MediaDetection.Win11MediaPlayer.Plugin());
        PluginFactory<INativeMediaPlayer>.Instance.LoadPlugin(new Plugins.MediaDetection.Generic.MpvPlugin());
        PluginFactory<INativeMediaPlayer>.Instance.LoadPlugin(new Plugins.MediaDetection.MpcHc.Plugin());
#endif

        return services;
    }

    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        services.AddSingleton<IAnimeSoundsService, AnimeSoundsService>();
        services.AddSingleton<IActivationService, ActivationService>();
        services.AddSingleton<IWinUINavigationService, NavigationService>();
        services.AddSingleton<INavigationService>(x => x.GetRequiredService<IWinUINavigationService>());
        services.AddSingleton<IAiredEpisodeToastService, AiredEpisodeToastService>();
        services.AddSingleton<IConnectivityService, ConnectivityService>();
        services.AddSingleton<IWindowService, WindowService>();

        // child navigation
        services.AddKeyedSingleton<IWinUINavigationService, NavigationService>(nameof(DiscoverViewModel));
        services.AddKeyedSingleton<IWinUINavigationService, NavigationService>(nameof(TorrentingViewModel));
        services.AddKeyedSingleton<IWinUINavigationService, NavigationService>(nameof(AboutAnimeViewModel));

        services.AddTransient<INavigationViewService, NavigationViewService>();
        services.AddTransient<IContentDialogService, ContentDialogService>();
        services.AddTransient<IToastService, ToastService>();
        services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();
        services.AddTransient<IViewService, ViewService>();
        services.AddTransient<PipWindow>();
        services.AddTransient<IMediaPlayerFactory, MediaPlayerFactory>();

        // media players
        services.AddMediaPlayer<WinUIMediaPlayerWrapper>();
        //services.AddMediaPlayer<LibVLCMediaPlayerWrapper>();
        services.AddMediaPlayer<FlyleafMediaPlayerWrapper>();


        return services;
    }

    public static IServiceCollection AddTopLevelPages(this IServiceCollection services)
    {
        services.AddCommonPages();
        
        services.AddPageForNavigation<UserListViewModel, UserListPage>();
        services.AddPageForNavigation<WatchViewModel, WatchPage>();
        services.AddPageForNavigation<SeasonalViewModel, SeasonalPage>();
        services.AddPageForNavigation<DiscoverViewModel, DiscoverPage>();
        services.AddPageForNavigation<AboutAnimeViewModel, AboutAnimePage>();
        services.AddPageForNavigation<TorrentingViewModel, TorrentingView>();
        services.AddPageForNavigation<NowPlayingViewModel, NowPlayingPage>(true);
        services.AddPageForNavigation<DiscoverMangaViewModel, DiscoverMangaPage>();
        services.AddPageForNavigation<ReadViewModel, ReadPage>();

        services.AddChildPages();

        return services;
    }

    private static IServiceCollection AddChildPages(this IServiceCollection services)
    {
        // Discover
        services.AddPageForNavigation<RecentEpisodesViewModel, RecentEpisodesSection>();
        services.AddPageForNavigation<SearchProviderViewModel, SearchProviderSection>();
        services.AddPageForNavigation<MyAnimeListDiscoverViewModel, MyAnimeListDiscoverSection>();

        // Torrenting
        services.AddPageForNavigation<SearchTorrentViewModel, SearchSection>();
        services.AddPageForNavigation<TorrentDownloadsViewModel, DownloadsSection>();

        // About Anime
        services.AddPageForNavigation<AnimeCardListViewModel, AnimeCardListSection>();
        services.AddPageForNavigation<PreviewsViewModel, PreviewsSection>();
        services.AddPageForNavigation<OriginalSoundTracksViewModel, OriginalSoundTracksSection>();
        services.AddPageForNavigation<AnimeEpisodesTorrentViewModel, EpisodesTorrentsSection>();

        return services;
    }

    public static IServiceCollection AddDialogPages(this IServiceCollection services)
    {
        services.AddPage<UpdateAnimeStatusViewModel, UpdateAnimeStatusView>();
        services.AddPage<ChooseSearchResultViewModel, ChooseSearchResultView>();
        services.AddPage<AuthenticateMyAnimeListViewModel, AuthenticateMyAnimeListView>();
        services.AddPage<AuthenticateAniListViewModel, AuthenticateAniListView>();
        services.AddPage<AuthenticateSimklViewModel, AuthenticateSimklView>();
        services.AddPage<PlayVideoDialogViewModel, VideoView>();
        services.AddPage<SubmitTimeStampsViewModel, SubmitTimeStampsView>();
        services.AddPage<ConfigureProviderViewModel, ConfigureProviderView>();
        services.AddPage<RequestRatingViewModel, RequestRatingView>();
        services.AddPage<PluginStoreViewModel, PluginStoreView>();
        services.AddPage<SearchListServiceViewModel, SearchListServicePage>();
        services.AddPage<EditAnimePreferencesViewModel, EditAnimePreferencesView>();

        return services;
    }

    private static IServiceCollection AddMediaPlayer<TMediaPlayer>(this IServiceCollection services)
        where TMediaPlayer : class, IMediaPlayer
    {
        services.AddTransient<TMediaPlayer>();
        services.AddTransient<Func<TMediaPlayer>>(x => () => x.GetRequiredService<TMediaPlayer>());
        return services;
    }
}
