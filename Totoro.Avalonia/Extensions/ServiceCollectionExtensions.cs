using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Totoro.Avalonia.Contracts;
using Totoro.Avalonia.Services;
using Totoro.Avalonia.Views;
using Totoro.Avalonia.Views.DiscoverSections;
using Totoro.Core.Contracts;
using Totoro.Core.ViewModels;
using Totoro.Core.ViewModels.Discover;

namespace Totoro.Avalonia.Extensions;

public static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddPageForNavigation<TViewModel, TView>(this IServiceCollection services, bool isSingleton = false)
        where TViewModel : class, INotifyPropertyChanged
        where TView : class, IViewFor<TViewModel>
    {
        services.AddPage<TViewModel, TView>(isSingleton);
        services.AddTransient<ViewType<TViewModel>>(x => new ViewType<TViewModel>(typeof(TView)));
        services.AddTransient<ViewType>(x => x.GetService<ViewType<TViewModel>>()!);
        return services;
    }
    
    private static IServiceCollection AddPage<TViewModel, TView>(this IServiceCollection services, bool isSingleton = false)
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

    public static IServiceCollection AddTopLevelPages(this IServiceCollection services)
    {
        services.AddPageForNavigation<SeasonalViewModel, SeasonalView>();
        services.AddPageForNavigation<DiscoverViewModel, DiscoverView>();
        services.AddPageForNavigation<UserListViewModel, UserListView>();
        services.AddPageForNavigation<WatchViewModelTest, WatchView>();
        
        // Discover views
        services.AddPageForNavigation<RecentEpisodesViewModel, RecentEpisodesView>();
        services.AddPageForNavigation<SearchProviderViewModel, SearchProviderView>();

        return services;
    }
    
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        //services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        //services.AddSingleton<IAnimeSoundsService, AnimeSoundsService>();
        //services.AddSingleton<IActivationService, ActivationService>();
        services.AddSingleton<IAvaloniaNavigationService, NavigationService>();
        services.AddSingleton<INavigationService>(x => x.GetRequiredService<IAvaloniaNavigationService>());
        services.AddSingleton<INavigationViewService, NavigationViewService>();
        //services.AddSingleton<IAiredEpisodeToastService, AiredEpisodeToastService>();
        services.AddSingleton<IConnectivityService, ConnectivityService>();
        //services.AddSingleton<IWindowService, WindowService>();

        // child navigation
        services.AddKeyedSingleton<IAvaloniaNavigationService, NavigationService>(nameof(DiscoverViewModel));
        services.AddKeyedSingleton<IAvaloniaNavigationService, NavigationService>(nameof(TorrentingViewModel));
        services.AddKeyedSingleton<IAvaloniaNavigationService, NavigationService>(nameof(AboutAnimeViewModel));

        //services.AddTransient<IContentDialogService, ContentDialogService>();
        //services.AddTransient<IToastService, ToastService>();
        //services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();
        services.AddTransient<IViewService, ViewService>();
        //services.AddTransient<PipWindow>();
        //services.AddTransient<IMediaPlayerFactory, MediaPlayerFactory>();

        // media players
        //services.AddMediaPlayer<WinUIMediaPlayerWrapper>();
        //services.AddMediaPlayer<LibVLCMediaPlayerWrapper>();
        //services.AddMediaPlayer<FlyleafMediaPlayerWrapper>();


        return services;
    }
}