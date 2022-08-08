using System.Collections.Generic;
using System.Threading.Tasks;
using AnimDL.Api;
using AnimDL.Core.Models;
using MalApi;

namespace AnimDL.WinUI.Contracts.Services;

public interface IViewService
{
    Task UpdateAnimeStatus(Anime anime);
    Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType);
    Task AuthenticateMal();
}
