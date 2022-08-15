using System.ComponentModel;
using AnimDL.UI.Core.Contracts;
using AnimDL.UI.Core.Services.MyAnimeList;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Services;
using AnimDL.WinUI.ViewModels;
using AnimDL.WinUI.Views;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace AnimDL.WinUI.Helpers;

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

    public static IServiceCollection AddMyAnimeList(this IServiceCollection services)
    {
        services.AddTransient<ITrackingService, MyAnimeListTrackingService>();
        services.AddTransient<IAnimeService, MyAnimeListService>();
        return services;
    }
}
