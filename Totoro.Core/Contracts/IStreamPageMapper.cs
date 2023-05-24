using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Contracts;

public interface IStreamPageMapper
{
    Task<(ICatalogItem Sub, ICatalogItem Dub)?> GetStreamPage(long malId, string provider);
    Task<long> GetId(string identifier, string provider);
    Task<long?> GetIdFromUrl(string url, string provider);
}
