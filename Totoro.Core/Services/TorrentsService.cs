using System.Reactive.Concurrency;
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
        ActiveDownlaods.Add(new TorrentModel(manager));
    }

    private async void Manager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
    {
        if (sender is not TorrentManager manager)
        {
            return;
        }

        if (e.NewState == TorrentState.Seeding)
        {
            manager.TorrentStateChanged -= Manager_TorrentStateChanged;
            await manager.StopAsync();
            RxApp.MainThreadScheduler.Schedule(() => ActiveDownlaods.Remove(ActiveDownlaods.FirstOrDefault(x => x.Downloader == manager)));
            await _engine.RemoveAsync(manager);
            _toastService.DownloadCompleted(manager.Torrent.Name);
        }
    }

    public ObservableCollection<TorrentModel> ActiveDownlaods { get; } = new();
}
