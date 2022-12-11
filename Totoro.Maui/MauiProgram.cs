using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core;
using Totoro.Core.Contracts;
using Totoro.Core.Models;
using Totoro.Core.ViewModels;
using Totoro.Maui.Services;
using Totoro.Maui.Views;

namespace Totoro.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            var a = Assembly.GetExecutingAssembly();
            using var stream = a.GetManifestResourceStream("Totoro.Maui.appsettings.json");
            var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
            builder.Configuration.AddConfiguration(config);

            builder.Services.AddTransient<DiscoverPage>();
            
            // fake services, need to fix
            builder.Services.AddTransient<INavigationService, ShellNavigationService>();
            builder.Services.AddTransient<ILocalSettingsService, LocalSettingsService>();
            builder.Services.AddSingleton<ISchedule, Schedule>();

            builder.Services.AddMyAnimeList(builder.Configuration);
            builder.Services.AddTotoro();
            builder.Services.AddViewModels();


            return builder.Build();
        }
    }

    public class LocalSettingsService : ILocalSettingsService
    {
        public string ApplicationDataFolder => throw new NotImplementedException();

        public T ReadSetting<T>(string key, T deafultValue = default)
        {
            return default(T);
        }

        public void SaveSetting<T>(string key, T value)
        {
            ;
        }
    }

    public class Schedule : ISchedule
    {
        public Task FetchSchedule()
        {
            return Task.CompletedTask;
        }

        public TimeRemaining GetTimeTillEpisodeAirs(long malId)
        {
            return new TimeRemaining(TimeSpan.Zero, DateTime.Now);
        }
    }
}