using MonoTorrent;
using MonoTorrent.Client;

namespace Totoro.Core.Services;

public class TorrentsService : ITorrentsService
{
    private readonly ClientEngine _engine = new(new());
    private readonly IToastService _toastService;

    public TorrentsService(IToastService toastService)
    {
        _toastService = toastService;
    }

    public async Task Download(Torrent torrent, string saveDirectory)
    {
        var manager = await _engine.AddAsync(torrent, saveDirectory);
        await manager.StartAsync();
        manager.TorrentStateChanged += Manager_TorrentStateChanged;
    }

    private async void Manager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
    {
        if (sender is not TorrentManager manager)
        {
            return;
        }

        if (e.NewState == TorrentState.Seeding && manager.Complete)
        {
            manager.TorrentStateChanged -= Manager_TorrentStateChanged;
            await manager.StopAsync();
            _toastService.DownloadCompleted(manager.Torrent.Name);
        }
    }

    public IList<TorrentManager> ActiveDownlaods => _engine.Torrents;
}
