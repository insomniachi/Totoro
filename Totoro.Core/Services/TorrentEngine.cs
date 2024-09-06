using MonoTorrent;
using MonoTorrent.Client;
using Splat;
using Totoro.Core.Contracts;

namespace Totoro.Core.Services;

public class TorrentEngine(IKnownFolders knownFolders,
                           ISettings settings,
                           ILocalSettingsService localSettingsService,
                           HttpClient httpClient) : ITorrentEngine, IEnableLogger
{
    private ClientEngine _engine;
    private readonly Dictionary<InfoHash, TorrentManager> _torrentManagers = [];
    private readonly Dictionary<string, TorrentManager> _torrentManagerToMagnetMap = [];
    private readonly string _torrentEngineState = Path.Combine(knownFolders.Torrents, "state.bin");
    private readonly ISettings _settings = settings;
    private readonly ILocalSettingsService _localSettingsService = localSettingsService;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ScheduledSubject<string> _torrentRemoved = new(RxApp.MainThreadScheduler);
    private readonly ScheduledSubject<TorrentManager> _torrentAdded = new(RxApp.MainThreadScheduler);
    private readonly List<InfoHash> _torrentDeleteRequests = localSettingsService.ReadSetting("TorrentDeleteRequests", new List<string>()).Select(InfoHash.FromHex).ToList();

    public IEnumerable<TorrentManager> TorrentManagers => _engine?.Torrents ?? Enumerable.Empty<TorrentManager>();
    public IObservable<string> TorrentRemoved => _torrentRemoved;
    public IObservable<TorrentManager> TorrentAdded => _torrentAdded;

    public async Task RemoveTorrent(string torrentName, bool removeFiles = false)
    {
        if (_engine.Torrents.FirstOrDefault(x => x.Torrent.Name == torrentName) is not { } tm)
        {
            return;
        }

        if (tm.State == TorrentState.Downloading)
        {
            await tm.StopAsync();
        }

        await RemoveTorrent(tm, removeFiles);
        _torrentRemoved.OnNext(tm.Torrent.Name);
    }

    public void MarkForDeletion(InfoHash infoHash)
    {
        if (!_settings.AutoRemoveWatchedTorrents)
        {
            return;
        }

        _torrentDeleteRequests.Add(infoHash);
        _localSettingsService.SaveSetting("TorrentDeleteRequests", _torrentDeleteRequests.Select(x => x.ToHex()));
    }

    public async Task<bool> TryRestoreState()
    {
        if (!File.Exists(_torrentEngineState))
        {
            _engine = new(new EngineSettingsBuilder() { CacheDirectory = Path.Combine(knownFolders.Torrents, "cache") }.ToSettings());
            return false;
        }

        try
        {
            _engine = await ClientEngine.RestoreStateAsync(_torrentEngineState);
            var torrentsRemoved = false;

            foreach (var item in _engine.Torrents)
            {
                if (_settings.AutoRemoveWatchedTorrents && _torrentDeleteRequests.Contains(item.InfoHashes.V1OrV2))
                {
                    await RemoveTorrent(item.Torrent.Name, true);
                    torrentsRemoved = true;
                }
                else
                {
                    SubscribeEvents(item);
                    _torrentManagers.Add(item.InfoHashes.V1OrV2, item);
                }
            }

            if (torrentsRemoved)
            {
                _torrentDeleteRequests.Clear();
                _localSettingsService.SaveSetting("TorrentDeleteRequests", _torrentDeleteRequests.Select(x => x.ToHex()));
            }
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
        }

        return true;
    }

    public async Task<TorrentManager> DownloadFromMagnet(string magnet, string saveDirectory)
    {
        try
        {
            var isNew = false;
            var magnetLink = MagnetLink.Parse(magnet);
            TorrentManager torrentManager = null;
            if (_torrentManagers.TryGetValue(magnetLink.InfoHashes.V1OrV2, out TorrentManager value))
            {
                torrentManager = value;
            }
            else
            {
                isNew = true;
                torrentManager = await _engine.AddStreamingAsync(magnetLink, saveDirectory);
                _torrentManagers.Add(magnetLink.InfoHashes.V1OrV2, torrentManager);
                SubscribeEvents(torrentManager);
            }

            if (!torrentManager.Complete)
            {
                await torrentManager.StartAsync();
            }

            if (!torrentManager.HasMetadata)
            {
                await torrentManager.WaitForMetadataAsync();
            }

            if (isNew)
            {
                _torrentAdded.OnNext(torrentManager);
            }

            return torrentManager;
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return null;
        }
    }

    public async Task<TorrentManager> DownloadFromUrl(string torrentUrl, string saveDirectory)
    {
        try
        {
            var stream = await _httpClient.GetStreamAsync(torrentUrl);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            var torrent = await Torrent.LoadAsync(ms);

            TorrentManager torrentManager = null;
            if (_torrentManagers.TryGetValue(torrent.InfoHashes.V1OrV2, out TorrentManager value))
            {
                torrentManager = value;
                _torrentManagerToMagnetMap.TryAdd(torrentUrl, torrentManager);
            }
            else
            {
                torrentManager = await _engine.AddStreamingAsync(torrent, saveDirectory);
                _torrentManagers.Add(torrent.InfoHashes.V1OrV2, torrentManager);
                _torrentManagerToMagnetMap.Add(torrentUrl, torrentManager);
                _torrentAdded.OnNext(torrentManager);
                SubscribeEvents(torrentManager);
            }

            if (!torrentManager.Complete)
            {
                await torrentManager.StartAsync();
            }

            return torrentManager;
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return null;
        }
    }

    public async Task<TorrentManager> Download(Torrent torrent, string saveDirectory)
    {
        try
        {
            TorrentManager torrentManager = null;
            if (_torrentManagers.TryGetValue(torrent.InfoHashes.V1OrV2, out TorrentManager value))
            {
                torrentManager = value;
            }
            else
            {
                torrentManager = await _engine.AddStreamingAsync(torrent, saveDirectory);
                _torrentManagers.Add(torrent.InfoHashes.V1OrV2, torrentManager);
                _torrentAdded.OnNext(torrentManager);
            }

            if (torrentManager.State != TorrentState.Downloading && !torrentManager.Complete)
            {
                await torrentManager.StartAsync();
            }

            return torrentManager;
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return null;
        }
    }

    public async Task<Stream> GetStream(string torrentUrl, int fileIndex)
    {
        var torrentManager = _torrentManagerToMagnetMap[torrentUrl];
        return torrentManager.Complete
            ? File.OpenRead(Path.Combine(torrentManager.Files[fileIndex].FullPath))
            : await torrentManager.StreamProvider.CreateStreamAsync(torrentManager.Files[fileIndex], _settings.PreBufferTorrents);
    }

    public async Task<Stream> GetStream(Torrent torrent, int fileIndex)
    {
        var torrentManager = _torrentManagers[torrent.InfoHashes.V1OrV2];
        return torrentManager.Complete
            ? File.OpenRead(Path.Combine(torrentManager.Files[fileIndex].FullPath))
            : await torrentManager.StreamProvider.CreateStreamAsync(torrentManager.Files[fileIndex], _settings.PreBufferTorrents);
    }

    public async Task SaveState()
    {
        if(_engine is null)
        {
            return;
        }

        await _engine.SaveStateAsync(_torrentEngineState);
    }

    private void SubscribeEvents(TorrentManager torrentManager)
    {
        torrentManager.TorrentStateChanged += TorrentManager_TorrentStateChanged;
        //torrentManager.PeerConnected += TorrentManager_PeerConnected;
        //torrentManager.PeerDisconnected += TorrentManager_PeerDisconnected;
    }

    private void TorrentManager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
    {
        this.Log().Info("{0} : {1} -> {2}", e.TorrentManager.Torrent?.Name ?? "Torrent State Changed", e.OldState, e.NewState);

        if (e.NewState is TorrentState.Seeding)
        {
            try
            {
                Task.Run(e.TorrentManager.StopAsync);
            }
            catch (Exception ex)
            {

                this.Log().Error(ex);
            }
        }
    }

    private async Task RemoveTorrent(TorrentManager torrentManager, bool removeFiles)
    {
        try
        {
            await _engine.RemoveAsync(torrentManager, removeFiles ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly);
            _torrentManagers.Remove(torrentManager.InfoHashes.V1OrV2);
        }
        catch (Exception ex)
        {

            this.Log().Error(ex);
        }

        if (!removeFiles)
        {
            return;
        }

        if (Directory.GetFiles(torrentManager.SavePath, "*", SearchOption.AllDirectories).Length == 0)
        {
            Directory.Delete(torrentManager.SavePath, true);
        }
    }

    //private void TorrentManager_PeerDisconnected(object sender, PeerDisconnectedEventArgs e)
    //{
    //    this.Log().Info($"Peer Disconnected : {e.Peer.Uri}");
    //}

    //private void TorrentManager_PeerConnected(object sender, PeerConnectedEventArgs e)
    //{
    //    this.Log().Info($"Peer Connected : {e.Peer.Uri}");
    //}

}
