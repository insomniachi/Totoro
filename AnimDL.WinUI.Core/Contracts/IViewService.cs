using AnimDL.Api;
using AnimDL.Core.Models;
using MalApi;

namespace AnimDL.WinUI.Contracts;

public interface IViewService
{
    Task UpdateAnimeStatus(Anime anime);
    Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType);
    Task AuthenticateMal();
}
