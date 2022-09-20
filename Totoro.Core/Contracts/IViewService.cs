using AnimDL.Api;

namespace Totoro.Core.Contracts;

public interface IViewService
{
    Task UpdateAnimeStatus(AnimeModel anime);
    Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType);
    Task AuthenticateMal();
    Task PlayVideo(string title, string url);
}
