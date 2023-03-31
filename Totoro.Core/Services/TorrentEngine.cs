using MonoTorrent;
using MonoTorrent.Client;
using Splat;

namespace Totoro.Core.Services;

public class TorrentEngine : ITorrentEngine, IEnableLogger
{
    private ClientEngine _engine;
    private readonly Dictionary<string, TorrentManager> _torrentManagers = new();
    private readonly string _torrentEngineState;
    private readonly ISettings _settings;
    private readonly HttpClient _httpClient;

    public TorrentEngine(IKnownFolders knownFolders,
                         ISettings settings,
                         HttpClient httpClient)
    {
        _torrentEngineState = Path.Combine(knownFolders.Torrents, "state.bin");
        _engine = new(new EngineSettingsBuilder() { CacheDirectory = Path.Combine(knownFolders.Torrents, "cache") }.ToSettings());
        _settings = settings;
        _httpClient = httpClient;
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
                await _engine.RemoveAsync(item, RemoveMode.CacheDataAndDownloadedData);

                if (!Directory.GetFiles(item.SavePath, "*", SearchOption.AllDirectories).Any())
                {
                    Directory.Delete(item.SavePath, true);
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
            if (_torrentManagers.TryGetValue(torrentUrl, out TorrentManager value))
            {
                return value;
            }

            var stream = await _httpClient.GetStreamAsync(torrentUrl);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            var torrent = await Torrent.LoadAsync(ms);
            var torrentManager = await _engine.AddStreamingAsync(torrent, saveDirectory);
            SubscribeEvents(torrentManager);
            await torrentManager.StartAsync();
            _torrentManagers.Add(torrentUrl, torrentManager);

            return torrentManager;
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            throw;
        }
    }

    public async Task<Stream> GetStream(string torrentUrl, int fileIndex)
    {
        var torrentManager = _torrentManagers[torrentUrl];
        return await torrentManager.StreamProvider.CreateStreamAsync(torrentManager.Files[fileIndex], _settings.PreBufferTorrents);
    }

    public async Task ShutDown()
    {
        await _engine.SaveStateAsync(_torrentEngineState);
    }

    private void SubscribeEvents(TorrentManager torrentManager)
    {
        torrentManager.TorrentStateChanged += TorrentManager_TorrentStateChanged;
        torrentManager.PeerConnected += TorrentManager_PeerConnected;
        torrentManager.PeerDisconnected += TorrentManager_PeerDisconnected;
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

    private void TorrentManager_PeerDisconnected(object sender, PeerDisconnectedEventArgs e)
    {
        this.Log().Info($"Peer Disconnected : {e.Peer.Uri}");
    }

    private void TorrentManager_PeerConnected(object sender, PeerConnectedEventArgs e)
    {
        this.Log().Info($"Peer Connected : {e.Peer.Uri}");
    }

}

