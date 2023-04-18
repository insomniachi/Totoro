using MonoTorrent;
using MonoTorrent.Client;

namespace Totoro.Core.Contracts;

public interface ITorrentEngine
{
    Task<TorrentManager> DownloadFromUrl(string torrentUrl, string saveDirectory);
    Task<TorrentManager> Download(Torrent torrent, string saveDirectory);
    Task<TorrentManager> DownloadFromMagnet(string magnet, string saveDirectory);
    Task<Stream> GetStream(string torrentUrl, int fileIndex);
    Task<Stream> GetStream(Torrent torrent, int fileIndex);
    Task SaveState();
    Task<bool> TryRestoreState();
    Task RemoveTorrent(string torrentName, bool removeFiles);
    void MarkForDeletion(InfoHash infoHash);
    IObservable<string> TorrentRemoved { get; }
    IObservable<TorrentManager> TorrentAdded { get; }
    IEnumerable<TorrentManager> TorrentManagers { get; }
}
