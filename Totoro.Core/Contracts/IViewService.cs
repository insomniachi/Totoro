namespace Totoro.Core.Contracts;

public interface IViewService
{
    Task<Unit> UpdateTracking(IAnimeModel anime);
    Task<SearchResult> ChoooseSearchResult(SearchResult closesMatch, List<SearchResult> searchResults, string providerType);
    Task Authenticate(ListServiceType type);
    Task PlayVideo(string title, string url);
    Task<T> SelectModel<T>(IEnumerable<T> models, T defaultValue = default, Func<string, IObservable<IEnumerable<T>>> searcher = default) where T : class;
    Task<long?> TryGetId(string title);
    Task SubmitTimeStamp(long malId, int ep, VideoStream stream, AniSkipResult existingResult, double duration, double introStart);
    Task<bool> Question(string title, string message);
    Task<Unit> Information(string title, string message);
    Task<Unit> ConfigureProvider(ProviderInfo providerType);
    Task<Unit> ConfigureOptions<T>(T keyType, Func<T, ProviderOptions> getFunc, Action<T, ProviderOptions> saveFunc);
}
