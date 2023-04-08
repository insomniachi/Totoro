using MonoTorrent.Client;

namespace Totoro.Core.Contracts;

public interface ITorrentEngine
{
    Task<TorrentManager> Download(string torrentUrl, string saveDirectory);
    Task<Stream> GetStream(string torrentUrl, int fileIndex);
    Task ShutDown();
    Task<bool> TryRestoreState();
    Task RemoveTorrent(string torrentName);
    IEnumerable<string> ActiveTorrents { get; }
    IObservable<string> TorrentRemoved { get; }
}
