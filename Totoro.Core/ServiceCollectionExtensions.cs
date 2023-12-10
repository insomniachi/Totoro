using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using Totoro.Core.Services;
using Totoro.Core.Services.AniList;
using Totoro.Core.Services.Debrid;
using Totoro.Core.Services.MediaEvents;
using Totoro.Core.Services.MyAnimeList;
using Totoro.Core.Services.ShanaProject;
using Totoro.Core.Services.Simkl;
using Totoro.Core.Services.StreamResolvers;
using Totoro.Core.ViewModels;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Manga;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.Core
{
    public static class ServiceCollectionExtensions
    {

        public static IServiceCollection AddTotoro(this IServiceCollection services)
        {
            services.AddSingleton<IDiscordRichPresense, DiscordRichPresense>();
            services.AddSingleton<IResumePlaybackService, PlaybackStateStorage>();
            services.AddSingleton<IVolatileStateStorage, VolatileStateStorage>();
            services.AddSingleton<ILocalMediaService, LocalMediaService>();
            services.AddSingleton<IAiredEpisodeNotifier, AiredEpisodeNotifier>();
            services.AddSingleton<IUpdateService, WindowsUpdateService>();
            services.AddSingleton<ITrackingServiceContext, TrackingServiceContext>();
            services.AddSingleton<IAnimeServiceContext, AnimeServiceContext>();
            services.AddSingleton<ISettings, SettingsModel>();
            services.AddSingleton<IPluginManager, PluginManager>();
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IKnownFolders, KnownFolders>();
            services.AddSingleton<IInitializer, Initalizer>();
            services.AddSingleton<ITorrentEngine, TorrentEngine>();
            services.AddSingleton<IRssDownloader, RssDownloader>();
            services.AddSingleton<INameService, NameService>();
            services.AddSingleton<IOfflineAnimeIdService, OfflineAnimeIdService>();

            services.AddTransient<IFileService, FileService>();
            services.AddTransient<ITimestampsService, TimestampsService>();
            services.AddTransient<IAnimeIdService, AnimeIdService>();
            services.AddTransient<IShanaProjectService, ShanaProjectService>();
            services.AddTransient<TotoroCommands>();
            services.AddTransient<ISystemClock, SystemClock>();
            services.AddTransient<ISchedulerProvider, SchedulerProvider>();
            services.AddTransient<IStreamPageMapper, StreamPageMapper>();
            services.AddTransient<IAnilistService, AnilistService>();
            services.AddTransient<IMyAnimeListService, MyAnimeListService>();
            services.AddTransient<ISimklService, SimklService>();
            services.AddTransient<IVideoStreamResolverFactory, VideoStreamResolverFactory>();
            services.AddTransient<IAnimeDetectionService, AnimeDetectionService>();

            services.AddTransient<IMediaEventListener, MediaSessionStateStorage>();
            services.AddTransient<IMediaEventListener, TrackingUpdater>();
            services.AddTransient<IMediaEventListener, DiscordRichPresenseUpdater>();
            services.AddTransient<IMediaEventListener, Aniskip>();

            services.AddMemoryCache();
            services.AddHttpClient();

            return services;
        }

        public static IServiceCollection AddTracking(this IServiceCollection services)
        {
            return services.AddMyAnimeList()
                           .AddAniList()
                           .AddSimkl();
        }

        public static IServiceCollection AddAniList(this IServiceCollection services)
        {
            services.AddTransient<ITrackingService, AniListTrackingService>();
            services.AddTransient<IAnimeService>(x => x.GetRequiredService<IAnilistService>());

            return services;
        }

        public static IServiceCollection AddMyAnimeList(this IServiceCollection services)
        {
            services.AddTransient<ITrackingService, MyAnimeListTrackingService>();
            services.AddTransient<IAnimeService, MyAnimeListService>();
            services.AddSingleton<IMalClient, MalClient>();

            return services;
        }

        public static IServiceCollection AddSimkl(this IServiceCollection services)
        {
            services.AddTransient<SimklHttpMessageHandler>();
            services.AddRefitClient<ISimklClient>()
                    .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://api.simkl.com"))
                    .AddHttpMessageHandler<SimklHttpMessageHandler>();
            services.AddTransient<ITrackingService, SimklTrackingService>();
            services.AddTransient<IAnimeService, SimklAnimeService>();

            return services;
        }

        public static IServiceCollection AddTorrenting(this IServiceCollection services)
        {
            services.AddTransient<IDebridService, PremiumizeService>();
            services.AddSingleton<IDebridServiceContext, DebridServiceContext>();

            return services;
        }

        public static IServiceCollection AddPlugins(this IServiceCollection services)
        {

#if DEBUG
            // anime
            PluginFactory<AnimeProvider>.Instance.LoadPlugin(new Plugins.Anime.AnimePahe.Plugin());
            PluginFactory<AnimeProvider>.Instance.LoadPlugin(new Plugins.Anime.AllAnime.Plugin());
            PluginFactory<AnimeProvider>.Instance.LoadPlugin(new Plugins.Anime.YugenAnime.Plugin());
            PluginFactory<AnimeProvider>.Instance.LoadPlugin(new Plugins.Anime.GogoAnime.Plugin());
            PluginFactory<AnimeProvider>.Instance.LoadPlugin(new Plugins.Anime.Marin.Plugin());
            PluginFactory<AnimeProvider>.Instance.LoadPlugin(new Plugins.Anime.AnimeSaturn.Plugin());
            PluginFactory<AnimeProvider>.Instance.LoadPlugin(new Plugins.Anime.WitAnime.Plugin());
            PluginFactory<AnimeProvider>.Instance.LoadPlugin(new Plugins.Anime.Aniwave.Plugin());

            // manga
            PluginFactory<MangaProvider>.Instance.LoadPlugin(new Plugins.Manga.MangaDex.Plugin());

            // torrents
            PluginFactory<ITorrentTracker>.Instance.LoadPlugin(new Plugins.Torrents.Nya.Plugin());
            PluginFactory<ITorrentTracker>.Instance.LoadPlugin(new Plugins.Torrents.AnimeTosho.Plugin());

#endif
            services.AddSingleton(typeof(IPluginOptionsStorage<>), typeof(PluginOptionStorage<>));
            services.AddSingleton<PluginOptionsStorage>();

            services.AddSingleton<IPluginFactory<AnimeProvider>>(PluginFactory<AnimeProvider>.Instance);
            services.AddSingleton<IPluginFactory<MangaProvider>>(PluginFactory<MangaProvider>.Instance);
            services.AddSingleton<IPluginFactory<ITorrentTracker>>(PluginFactory<ITorrentTracker>.Instance);

            return services;
        }
    }
}
