namespace Totoro.Core.Contracts;

public interface IViewService
{
    Task<Unit> UpdateTracking(IAnimeModel anime);
    Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType);
    Task AuthenticateMal();
    Task PlayVideo(string title, string url);
    Task<T> SelectModel<T>(IEnumerable<object> models) where T : class;
    Task SubmitTimeStamp(long malId, int ep, VideoStream stream, double duration, double introStart);
}
