using System.Reactive.Concurrency;
using MonoTorrent;
using MonoTorrent.Client;
using Splat;

namespace Totoro.Core.Services;

public class TorrentsService : ITorrentsService, IEnableLogger
{
    private readonly ClientEngine _engine = new(new());
    private readonly IToastService _toastService;
    private readonly ILocalMediaService _localMediaService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IViewService _viewService;
    private readonly IAnimeServiceContext _animeService;
    private readonly string _mediaFolder;

    public TorrentsService(IToastService toastService,
                           ILocalMediaService localMediaService,
                           ILocalSettingsService localSettingsService,
                           IViewService viewService,
                           IAnimeServiceContext animeService)
    {
        _toastService = toastService;
        _localMediaService = localMediaService;
        _localSettingsService = localSettingsService;
        _viewService = viewService;
        _animeService = animeService;
        _mediaFolder = _localSettingsService.ReadSetting("MediaFolder", Path.Combine(_localSettingsService.ApplicationDataFolder, "Media"));
    }

    public async Task Download(IDownloadableContent content)
    {
        var saveDirectory = Path.Combine(_mediaFolder, content.Title);

        if (!Directory.Exists(saveDirectory))
        {
            // ask for id
            var id = await _viewService.TryGetId(content.Title);
            if (id is { })
            {
                _localMediaService.SetId(saveDirectory, id.Value);
            }
        }

        var torrent = Torrent.Load(new Uri(content.Url), Path.Combine(_localSettingsService.ApplicationDataFolder, "temp.torrent"));
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
            _toastService.DownloadCompleted(manager.ContainingDirectory, manager.Torrent.Name);
        }
    }

    public ObservableCollection<TorrentModel> ActiveDownlaods { get; } = new();
}
