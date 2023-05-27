using Totoro.Plugins.Torrents.Models;

namespace Totoro.Plugins.Torrents.Contracts;

public interface ITorrentTracker
{
    IAsyncEnumerable<TorrentModel> Recents();
    IAsyncEnumerable<TorrentModel> Search(string query);
}
