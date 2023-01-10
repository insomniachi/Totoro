using System.Reactive;
using System.Reflection;
using AnimDL.Core;
using AnimDL.Core.Api;
using AnimDL.Core.Models;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
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

            builder.Services.AddTransient<LoginMALPage>();
            builder.Services.AddTransient<Func<LoginMALPage>>(x => x.GetRequiredService<LoginMALPage>);
            builder.Services.AddTransient<LoginMALViewModel>();
            builder.Services.AddSingleton<ISettings, SettingsViewModel>();
            
            // fake services, need to fix
            builder.Services.AddTransient<INavigationService, ShellNavigationService>();
            builder.Services.AddSingleton<ISchedule, Schedule>();
            builder.Services.AddTransient<IViewService, ViewService>();
            builder.Services.AddTransient<IThemeSelectorService, ThemeSelectorService>();


            builder.Services.AddAnimDL();
            builder.Services.AddMyAnimeList();
            builder.Services.AddTotoro();

            builder.Services.AddTransientWithShellRoute<DiscoverPage, DiscoverViewModel>(nameof(DiscoverViewModel));
            builder.Services.AddTransientWithShellRoute<UserListPage, UserListViewModel>(nameof(UserListViewModel));


            return builder.Build();
        }
    }

    public class ViewService : IViewService
    {
        public Task Authenticate(ListServiceType type)
        {
            throw new NotImplementedException();
        }

        public Task AuthenticateMal()
        {
            return Task.CompletedTask;
        }

        public Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType)
        {
            return Task.FromResult(searchResults.FirstOrDefault());
        }

        public Task<SearchResult> ChoooseSearchResult(SearchResult closesMatch, List<SearchResult> searchResults, ProviderType providerType)
        {
            throw new NotImplementedException();
        }

        public Task<Unit> Information(string title, string message)
        {
            throw new NotImplementedException();
        }

        public Task PlayVideo(string title, string url)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Question(string title, string message)
        {
            throw new NotImplementedException();
        }

        public Task<T> SelectModel<T>(IEnumerable<object> models) where T : class
        {
            throw new NotImplementedException();
        }

        public Task SubmitTimeStamp(long malId, int ep, VideoStream stream, double duration, double introStart)
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

    public class ThemeSelectorService : IThemeSelectorService
    {
        public ElementTheme Theme => ElementTheme.Default;

        public void Initialize()
        {
        }

        public void SetRequestedTheme()
        {
        }

        public void SetTheme(ElementTheme theme)
        {
        }
    }
}