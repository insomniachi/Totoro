using MonoTorrent;

namespace Totoro.Core.Contracts;

public interface ITorrentEngine
{
    Task<Torrent> Download(string torrentUrl, string saveDirectory);
    Task<Stream> GetStream(string torrentUrl, int fileIndex);
    Task ShutDown();
}
