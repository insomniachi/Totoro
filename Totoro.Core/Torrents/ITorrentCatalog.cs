namespace Totoro.Core.Torrents;

public interface ITorrentCatalog
{
    IAsyncEnumerable<TorrentModel> Recents();
    IAsyncEnumerable<TorrentModel> Search(string query);
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
