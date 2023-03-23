using MonoTorrent;
using MonoTorrent.Client;
using Splat;

namespace Totoro.Core.Services;

public class TorrentEngine : ITorrentEngine, IEnableLogger
{
    private readonly ClientEngine _engine = new(new());
    private readonly Dictionary<string, TorrentManager> _torrentManagers = new();
    private readonly string _tempTorrent;

    public TorrentEngine(IKnownFolders knownFolders)
    {
        _tempTorrent = Path.Combine(knownFolders.Torrents, "temp.torrent");
    }

    public async Task<Torrent> Download(string torrentUrl, string saveDirectory)
    {
        try
        {
            if(_torrentManagers.TryGetValue(torrentUrl, out TorrentManager value))
            {
                return value.Torrent;
            }

            var torrent = await Torrent.LoadAsync(new Uri(torrentUrl), _tempTorrent);
            File.Delete(_tempTorrent);
            var torrentManager = await _engine.AddStreamingAsync(torrent, saveDirectory);
            torrentManager.TorrentStateChanged += TorrentManager_TorrentStateChanged;
            await torrentManager.StartAsync();
            _torrentManagers.Add(torrentUrl, torrentManager);

            return torrentManager.Torrent;
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
        return await torrentManager.StreamProvider.CreateStreamAsync(torrentManager.Files[fileIndex], false);
    }

    public async Task ShutDown()
    {
        try
        {
            foreach (var manager in _torrentManagers.Values)
            {
                await manager.StopAsync();
                await _engine.RemoveAsync(manager, RemoveMode.CacheDataAndDownloadedData);
                if (!Directory.GetFiles(manager.ContainingDirectory, "*", SearchOption.AllDirectories).Any())
                {
                    Directory.Delete(manager.ContainingDirectory, true);
                }
            }
            _torrentManagers.Clear();
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
        }
    }

    private void TorrentManager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
    {
        this.Log().Info("Torrent State Changed : {0} -> {1}", e.OldState, e.NewState);
    }
}

