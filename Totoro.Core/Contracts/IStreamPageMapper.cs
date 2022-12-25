using AnimDL.Api;

namespace Totoro.Core.Contracts;

public interface IStreamPageMapper
{
    Task<(SearchResult Sub, SearchResult Dub)?> GetStreamPage(long malId, ProviderType provider);
    Task<long> GetMalId(string identifier, ProviderType provider);
    Task<long> GetMalIdFromUrl(string url, ProviderType provider);
}
