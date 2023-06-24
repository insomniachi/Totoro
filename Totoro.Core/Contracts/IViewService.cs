using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Core.Contracts;

public interface IViewService
{
    Task<Unit> UpdateTracking(IAnimeModel anime);
    Task<int> RequestRating(IAnimeModel anime);
    Task<ICatalogItem> ChoooseSearchResult(ICatalogItem closesMatch, List<ICatalogItem> searchResults, string providerType);
    Task Authenticate(ListServiceType type);
    Task PlayVideo(string title, string url);
    Task<T> SelectModel<T>(IEnumerable<T> models, T defaultValue = default, Func<string, IObservable<IEnumerable<T>>> searcher = default) where T : class;
    Task<long?> TryGetId(string title);
    Task<long?> BeginTryGetId(string title);
    Task SubmitTimeStamp(long malId, int ep, VideoStreamModel stream, AniSkipResult existingResult, double duration, double introStart);
    Task<bool> Question(string title, string message);
    Task<Unit> Information(string title, string message);
    Task<Unit> ConfigureProvider(PluginInfo providerType);
    Task<Unit> ConfigureOptions<T>(T keyType, Func<T, PluginOptions> getFunc, Action<T, PluginOptions> saveFunc);
    Task<string> BrowseFolder();
    Task<string> BrowseSubtitle();
    Task UnhandledException(Exception ex);
}
