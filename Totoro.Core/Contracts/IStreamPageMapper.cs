namespace Totoro.Core.Contracts;

public interface IStreamPageMapper
{
    Task<(SearchResult Sub, SearchResult Dub)?> GetStreamPage(long malId, string provider);
    Task<long> GetId(string identifier, string provider);
    Task<long?> GetIdFromUrl(string url, string provider);
}
