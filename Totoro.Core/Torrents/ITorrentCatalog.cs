using System.Diagnostics;

namespace Totoro.Core.Torrents;

public enum TorrentProviderType
{
    Nya,
    AnimeTosho
}

public interface ITorrentCatalog
{
    IAsyncEnumerable<TorrentModel> Recents();
    IAsyncEnumerable<TorrentModel> Search(string query);
}

public interface ITorrentCatalogFactory
{
    ITorrentCatalog GetCatalog(TorrentProviderType type);
}

public class TorrentCatalogFactory : ITorrentCatalogFactory
{
    private readonly IEnumerable<ITorrentCatalog> _torrentCatalogs;

    public TorrentCatalogFactory(IEnumerable<ITorrentCatalog> torrentCatalogs)
    {
        _torrentCatalogs = torrentCatalogs;
    }

    public ITorrentCatalog GetCatalog(TorrentProviderType type)
    {
        return type switch
        {
            TorrentProviderType.Nya => _torrentCatalogs.First(x => x is NyaaCatalog),
            TorrentProviderType.AnimeTosho => _torrentCatalogs.First(x => x is AnimeToshoCatalog),
            _ => throw new UnreachableException()
        };
    }
}

public interface IIndexedTorrentCatalog
{
    IAsyncEnumerable<TorrentModel> Search(string query, AnimeId id);
}

public enum TorrentState
{
    Unknown,
    NotCached,
    Requested
}

public class TorrentModel : ReactiveObject
{
    public string Name { get; set; }
    public string Link { get; set; }
    public string MagnetLink { get; set; }
    public string Size { get; set; }
    public DateTime Date { get; set; }
    public int Seeders { get; set; }
    public int Leeches { get; set; }
    public int Completed { get; set; }
    public string CategoryImage { get; set; }
    public string Category { get; set; }
    [Reactive] public TorrentState State { get; set; }
}
