using AnimDL.Api;
using AnimDL.Core.Models;
using AnimDL.UI.Core.Models;

namespace AnimDL.UI.Core.Contracts;

public interface IViewService
{
    Task UpdateAnimeStatus(AnimeModel anime);
    Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType);
    Task AuthenticateMal();
}
