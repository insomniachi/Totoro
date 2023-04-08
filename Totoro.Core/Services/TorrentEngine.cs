using MonoTorrent;
using MonoTorrent.Client;
using Splat;

namespace Totoro.Core.Services;

public class TorrentEngine : ITorrentEngine, IEnableLogger
{
    private ClientEngine _engine;
    private readonly Dictionary<InfoHash, TorrentManager> _torrentManagers = new();
    private readonly Dictionary<string, TorrentManager> _torrentManagerToMagnetMap = new();
    private readonly string _torrentEngineState;
    private readonly ISettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ScheduledSubject<string> _torrentRemoved = new(RxApp.MainThreadScheduler);

    public TorrentEngine(IKnownFolders knownFolders,
                         ISettings settings,
                         HttpClient httpClient)
    {
        _torrentEngineState = Path.Combine(knownFolders.Torrents, "state.bin");
        _engine = new(new EngineSettingsBuilder() { CacheDirectory = Path.Combine(knownFolders.Torrents, "cache") }.ToSettings());
        _settings = settings;
        _httpClient = httpClient;
    }

    public IEnumerable<string> ActiveTorrents => _engine.Torrents.Select(x => x.Torrent.Name);
    
    public IObservable<string> TorrentRemoved => _torrentRemoved;

    public async Task RemoveTorrent(string torrentName)
    {
        if(_engine.Torrents.FirstOrDefault(x => x.Torrent.Name == torrentName) is not { } tm)
        {
            return;
        }

        if(tm.State == TorrentState.Downloading)
        {
            await tm.StopAsync();
        }

        await RemoveTorrent(tm);
        _torrentRemoved.OnNext(tm.Torrent.Name);
    }

    public async Task<bool> TryRestoreState()
    {
        if (!File.Exists(_torrentEngineState))
        {
            return false;
        }

        try
        {
            _engine = await ClientEngine.RestoreStateAsync(_torrentEngineState);
            File.Delete(_torrentEngineState);

            foreach (var item in _engine.Torrents)
            {
                if(item.Complete && _settings.AutoRemoveCompletedTorrents)
                {
                    await RemoveTorrent(item);
                }
                else if(item.Complete == false)
                {
                    _torrentManagers.Add(item.InfoHash, item);
                }
            }
        }
        catch { }

        return true;
    }

    public async Task<TorrentManager> Download(string torrentUrl, string saveDirectory)
    {
        try
        {
            var stream = await _httpClient.GetStreamAsync(torrentUrl);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            var torrent = await Torrent.LoadAsync(ms);

            TorrentManager torrentManager = null;
            if (_torrentManagers.TryGetValue(torrent.InfoHash, out TorrentManager value))
            {
                torrentManager = value;
                _torrentManagerToMagnetMap.TryAdd(torrentUrl, torrentManager);
            }
            else
            {
                torrentManager = await _engine.AddStreamingAsync(torrent, saveDirectory);
                _torrentManagers.Add(torrent.InfoHash, torrentManager);
                _torrentManagerToMagnetMap.Add(torrentUrl, torrentManager);
                SubscribeEvents(torrentManager);
            }

            await torrentManager.StartAsync();

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
        return await torrentManager.StreamProvider.CreateStreamAsync(torrentManager.Files[fileIndex], _settings.PreBufferTorrents);
    }

    public async Task ShutDown()
    {
        await _engine.StopAllAsync();
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
        this.Log().Info("Torrent State Changed : {0} -> {1}", e.OldState, e.NewState);

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

    private async Task RemoveTorrent(TorrentManager torrentManager)
    {
        await _engine.RemoveAsync(torrentManager, RemoveMode.CacheDataAndDownloadedData);

        if (!Directory.GetFiles(torrentManager.SavePath, "*", SearchOption.AllDirectories).Any())
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
