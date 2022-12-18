using System.Reactive;
using System.Reflection;
using AnimDL.Api;
using AnimDL.Core.Models;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core;
using Totoro.Core.Contracts;
using Totoro.Core.Models;
using Totoro.Core.ViewModels;
using Totoro.Maui.Services;
using Totoro.Maui.ViewModels;
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
                })
                .UseMauiCommunityToolkit();

            var a = Assembly.GetExecutingAssembly();
            using var stream = a.GetManifestResourceStream("Totoro.Maui.appsettings.json");
            var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
            builder.Configuration.AddConfiguration(config);

            builder.Services.AddTransient<DiscoverPage>();
            builder.Services.AddTransient<LoginMALPage>();
            builder.Services.AddTransient<Func<LoginMALPage>>(x => x.GetRequiredService<LoginMALPage>);
            builder.Services.AddTransient<LoginMALViewModel>();
            builder.Services.AddTransient<UserListPage>();
            
            // fake services, need to fix
            builder.Services.AddTransient<INavigationService, ShellNavigationService>();
            builder.Services.AddSingleton<ISchedule, Schedule>();
            builder.Services.AddTransient<IViewService, ViewService>();

            builder.Services.AddMyAnimeList(builder.Configuration);
            builder.Services.AddTotoro();
            builder.Services.AddViewModels();

            Routing.RegisterRoute(nameof(UserListViewModel), typeof(UserListPage));


            return builder.Build();
        }
    }

    public class ViewService : IViewService
    {
        public Task AuthenticateMal()
        {
            return Task.CompletedTask;
        }

        public Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType)
        {
            return Task.FromResult(searchResults.FirstOrDefault());
        }

        public Task PlayVideo(string title, string url)
        {
            throw new NotImplementedException();
        }

        public Task<T> SelectModel<T>(IEnumerable<object> models) where T : class
        {
            throw new NotImplementedException();
        }

        public Task<Unit> UpdateTracking(IAnimeModel anime)
        {
            throw new NotImplementedException();
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