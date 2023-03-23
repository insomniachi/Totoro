using AnitomySharp;
using MonoTorrent;

namespace Totoro.Core.Services.StreamResolvers;

public sealed class MonoTorrentStreamModelResolver : IVideoStreamModelResolver, IAsyncDisposable
{
    private readonly ITorrentEngine _torrentEngine;
    private readonly string _torrentUrl;
    private readonly string _saveDirectory;
    private readonly Dictionary<int, int> _episodeToTorrentFileMap = new();
    private Torrent _torrent;

    public MonoTorrentStreamModelResolver(ITorrentEngine torrentEngine,
                                          IKnownFolders knownFolders,
                                          IEnumerable<Element> parsedResults,
                                          string torrentUrl)
    {
        _torrentEngine = torrentEngine;
        _torrentUrl = torrentUrl;
        var folder = parsedResults.First(x => x.Category == Element.ElementCategory.ElementAnimeTitle).Value;
        _saveDirectory = Path.Combine(knownFolders.Torrents, folder);
    }

    public async ValueTask DisposeAsync()
    {
        await _torrentEngine.ShutDown();
    }

    public async Task<EpisodeModelCollection> ResolveAllEpisodes(string subStream)
    {
        _torrent = await _torrentEngine.Download(_torrentUrl, _saveDirectory);

        var index = 0;
        foreach (var file in _torrent.Files.Select(x => x.Path))
        {
            var result = AnitomySharp.AnitomySharp.Parse(file);
            if(result.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeNumber) is { } epResult &&
                int.TryParse(epResult.Value, out var ep))
            {
                _episodeToTorrentFileMap[ep] = index;
            }

            index++;
        }

        var start = _episodeToTorrentFileMap.Keys.Min();
        var end = _episodeToTorrentFileMap.Keys.Max();

        return EpisodeModelCollection.FromEpisode(start, end);
    }

    public async Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, string subStream)
    {
        return new VideoStreamsForEpisodeModel(await _torrentEngine.GetStream(_torrentUrl, _episodeToTorrentFileMap[episode]));
    }
}

