namespace Totoro.Core.Contracts;

public interface IStreamPageMapper
{
    Task<(SearchResult Sub, SearchResult Dub)?> GetStreamPage(long malId, ProviderType provider);
    Task<long> GetId(string identifier, ProviderType provider);
    Task<long?> GetIdFromUrl(string url, ProviderType provider);
}
