using AnimDL.Api;

namespace Totoro.Core.Contracts;

public interface IStreamPageMapper
{
    Task<(SearchResult Sub, SearchResult Dub)?> GetStreamPage(long malId, ProviderType provider);
}
