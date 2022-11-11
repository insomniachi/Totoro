using MonoTorrent;
using MonoTorrent.Client;

namespace Totoro.Core.Contracts
{
    public interface ITorrentsService
    {
        IList<TorrentManager> ActiveDownlaods { get; }

        Task Download(Torrent torrent, string saveDirectory);
    }
}