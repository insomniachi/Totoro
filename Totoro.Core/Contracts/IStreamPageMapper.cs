using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Contracts;

public interface IStreamPageMapper
{
    Task<long?> GetIdFromUrl(string url, string provider);
}
