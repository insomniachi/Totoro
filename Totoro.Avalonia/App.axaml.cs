using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using Totoro.Avalonia.Contracts;
using Totoro.Avalonia.Extensions;
using Totoro.Avalonia.Services;
using Totoro.Avalonia.ViewModels;
using Totoro.Avalonia.Views;
using Totoro.Core;
using Totoro.Core.Contracts;
using Totoro.Core.ViewModels;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Manga;
using Totoro.Plugins.Torrents.Contracts;
using MainWindow = Totoro.Avalonia.Views.MainWindow;

namespace Totoro.Avalonia
{
    public partial class App : Application
    {
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(config => { config.AddJsonFile("appsettings.Development.json", optional: true); })
            .ConfigureServices((context, services) =>
            {
                services.AddPlatformServices()
                    .AddTotoro()
                    .AddPlugins()
                    .AddTracking()
                    .AddTorrenting()
                    .AddTopLevelPages();
                //.AddMediaDetection()
                //.AddDialogPages();

                services.AddSingleton(MessageBus.Current);
                services.AddTransient<DefaultExceptionHandler>();
                services.AddSingleton<IPluginManager>(x => new PluginManager(PluginFactory<AnimeProvider>.Instance,
                    PluginFactory<MangaProvider>.Instance,
                    PluginFactory<ITorrentTracker>.Instance,
                    null));

                // Configuration
                // services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
            })
            .Build();


        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public static IServiceProvider Services { get; private set; } = null!;

        public override void OnFrameworkInitializationCompleted()
        {
            Services = _host.Services;

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}