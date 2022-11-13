using MonoTorrent;

namespace Totoro.Core.Contracts
{
    public interface ITorrentsService
    {
        ObservableCollection<TorrentModel> ActiveDownlaods { get; }

        Task Download(Torrent torrent, string saveDirectory);
    }
}