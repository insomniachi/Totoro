using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Contracts;

public interface IViewService
{
    Task<Unit> UpdateTracking(IAnimeModel anime);
    Task<int> RequestRating(IAnimeModel anime);
    Task<ICatalogItem> ChoooseSearchResult(ICatalogItem closesMatch, List<ICatalogItem> searchResults, string providerType);
    Task Authenticate(ListServiceType type);
    Task PlayVideo(string title, string url);
    Task<T> SelectModel<T>(IEnumerable<T> models, T defaultValue = default, Func<string, IObservable<IEnumerable<T>>> searcher = default) where T : class;
    Task SubmitTimeStamp(long malId, int ep, VideoStreamModel stream, TimestampResult existingResult, double duration, double introStart);
    Task<bool> Question(string title, string message);
    Task<Unit> Information(string title, string message);
    Task<string> BrowseFolder();
    Task<string> BrowseSubtitle();
    Task UnhandledException(Exception ex);
    Task ShowPluginStore(string pluginType);
    Task PromptAnimeName(long id);
    Task ShowSearchListServiceDialog();
}
