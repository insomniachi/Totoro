using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Totoro.Core.Services;
using Totoro.Core.Services.AnimixPlay;
using Totoro.Core.Services.MyAnimeList;
using Totoro.Core.Services.ShanaProject;
using Totoro.Core.ViewModels;

namespace Totoro.Core
{
    public static class ServiceCollectionExtensions
    {

        public static IServiceCollection AddTotoro(this IServiceCollection services)
        {
            //services.AddSingleton<IDiscordRichPresense, DiscordRichPresense>();
            //services.AddSingleton<ISchedule, Schedule>();
            //services.AddSingleton<IAnimeSoundsService, AnimeSoundsService>();
            //services.AddTransient<IViewService, ViewService>();
            //services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();
            //services.AddTransient<IMediaPlayer, WinUIMediaPlayerWrapper>();

            services.AddSingleton<IPlaybackStateStorage, PlaybackStateStorage>();
            services.AddSingleton<IVolatileStateStorage, VolatileStateStorage>();
            services.AddSingleton<ITimestampsService, TimestampsService>();
            services.AddSingleton<ITorrentsService, TorrentsService>();
            services.AddSingleton<ILocalMediaService, LocalMediaService>();

            services.AddTransient<IFileService, FileService>();
            services.AddTransient<MalToModelConverter>();
            services.AddTransient<IRecentEpisodesProvider, AnimixPlayEpisodesProvider>();
            services.AddTransient<IFeaturedAnimeProvider, AnimixPlayFeaturedAnimeProvider>();
            services.AddTransient<IAnimeIdService, AnimeIdService>();
            services.AddTransient<IShanaProjectService, ShanaProjectService>();
            services.AddTransient<TotoroCommands>();
            services.AddTransient<ISystemClock, SystemClock>();
            services.AddTransient<ISchedulerProvider, SchedulerProvider>();

            services.AddMemoryCache();
            services.AddHttpClient();

            return services;
        }

        public static IServiceCollection AddMyAnimeList(this IServiceCollection services, HostBuilderContext context)
        {
            services.AddTransient<ITrackingService, MyAnimeListTrackingService>();
            services.AddTransient<IAnimeService, MyAnimeListService>();

            services.AddSingleton<IMalClient, MalClient>(x =>
            {
                var settingService = x.GetRequiredService<ILocalSettingsService>();
                var token = settingService.ReadSetting<OAuthToken>("MalToken");
                var clientId = context.Configuration["ClientId"];
                if ((DateTime.UtcNow - (token?.CreateAt ?? DateTime.UtcNow)).Days >= 28)
                {
                    token = MalAuthHelper.RefreshToken(clientId, token.RefreshToken).Result;
                    settingService.SaveSetting("MalToken", token);
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

        public static IServiceCollection AddViewModels(this IServiceCollection services)
        {
            services.AddTransient<AboutAnimeViewModel>();
            services.AddTransient<DiscoverViewModel>();
            services.AddTransient<DownloadViewModel>();
            services.AddTransient<ScheduleViewModel>();
            services.AddTransient<SeasonalViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<UserListViewModel>();
            services.AddTransient<WatchViewModel>();

            return services;
        }
    }
}
