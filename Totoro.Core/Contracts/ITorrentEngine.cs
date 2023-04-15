using MonoTorrent;
using MonoTorrent.Client;

namespace Totoro.Core.Contracts;

public interface ITorrentEngine
{
    Task<TorrentManager> Download(string torrentUrl, string saveDirectory);
    Task<TorrentManager> Download(Torrent torrent, string saveDirectory);
    Task<TorrentManager> DownloadFromMagnet(string magnet, string saveDirectory);
    Task<Stream> GetStream(string torrentUrl, int fileIndex);
    Task<Stream> GetStream(Torrent torrent, int fileIndex);
    Task ShutDown();
    Task<bool> TryRestoreState();
    Task RemoveTorrent(string torrentName, bool removeFiles);
    IObservable<string> TorrentRemoved { get; }
    IEnumerable<TorrentManager> TorrentManagers { get; }
}
