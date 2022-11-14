using System.Net.Mime;
using System.Reactive.Concurrency;
using FuzzySharp;
using Humanizer;
using MonoTorrent;
using MonoTorrent.Client;
using Totoro.Core.Contracts;

namespace Totoro.Core.Services;

public class TorrentsService : ITorrentsService
{
    private readonly ClientEngine _engine = new(new());
    private readonly IToastService _toastService;
    private readonly ILocalMediaService _localMediaService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IViewService _viewService;
    private readonly IAnimeService _animeService;
    private readonly string _mediaFolder;

    public TorrentsService(IToastService toastService,
                           ILocalMediaService localMediaService,
                           ILocalSettingsService localSettingsService,
                           IViewService viewService,
                           IAnimeService animeService)
    {
        _toastService = toastService;
        _localMediaService = localMediaService;
        _localSettingsService = localSettingsService;
        _viewService = viewService;
        _animeService = animeService;
        _mediaFolder = _localSettingsService.ReadSetting<string>("MediaFolder", Path.Combine(_localSettingsService.ApplicationDataFolder, "Media"));
    }

    public async Task Download(IDownloadableContent content)
    {
        var saveDirectory = Path.Combine(_mediaFolder, content.Title);
        
        if(!Directory.Exists(saveDirectory))
        {
            // ask for id
            var candidates = await _animeService.GetAnime(content.Title);
            var filtered = candidates.Where(x => Fuzz.PartialRatio(x.Title, content.Title) > 80 || x.AlternativeTitles.Any(x => Fuzz.PartialRatio(content.Title, x) > 80)).ToList();

            if(filtered.Count == 1)
            {
                _localMediaService.SetId(saveDirectory, filtered.First().Id);
            }
            else
            {
                var model = await _viewService.SelectModel<SearchResultModel>(filtered);
                _localMediaService.SetId(saveDirectory, model.Id);
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
