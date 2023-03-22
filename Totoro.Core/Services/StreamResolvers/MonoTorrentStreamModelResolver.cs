using AnitomySharp;
using MonoTorrent;
using MonoTorrent.Client;

namespace Totoro.Core.Services.StreamResolvers;

public sealed class MonoTorrentStreamModelResolver : IVideoStreamModelResolver, IAsyncDisposable
{
    private readonly ClientEngine _engine = new(new());
    private readonly IEnumerable<Element> _parsedResults;
    private readonly string _magnet;
    private readonly string _folder;
    private Torrent _torrent;
    private TorrentManager _torrentManager;

    public MonoTorrentStreamModelResolver(IKnownFolders knownFolders,
                                          IEnumerable<Element> parsedResults,
                                          string magnet)
    {
        _parsedResults = parsedResults;
        _magnet = magnet;
        var folder = parsedResults.First(x => x.Category == Element.ElementCategory.ElementAnimeTitle).Value;
        _folder = Path.Combine(knownFolders.Torrents, folder);
    }

    public async Task<EpisodeModelCollection> ResolveAllEpisodes(string subStream)
    {
        var magnet = MagnetLink.Parse(_magnet);
        var bytes = await _engine.DownloadMetadataAsync(magnet, CancellationToken.None);
        _torrent = Torrent.Load(bytes);
        _torrentManager = await _engine.AddStreamingAsync(_torrent, _folder);
        var epString = _parsedResults.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeNumber)?.Value ?? "1";
        var ep = int.Parse(epString);
        return EpisodeModelCollection.FromEpisode(ep);
    }

    public async Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, string subStream)
    {
        await _torrentManager.StartAsync(); 
        var stream = await _torrentManager.StreamProvider.CreateStreamAsync(_torrentManager.Files[0]);
        return new VideoStreamsForEpisodeModel(stream);
    }

    public async ValueTask DisposeAsync()
    {
        await _torrentManager.StopAsync();
    }
}

