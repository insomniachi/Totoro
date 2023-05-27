using AnitomySharp;
using MonoTorrent;
using MonoTorrent.Client;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.StreamResolvers;

public sealed class MonoTorrentStreamModelResolver : IVideoStreamModelResolver, 
                                                     INotifyDownloadStatus,
                                                     ICompletionAware,
                                                     IAsyncDisposable
{
    private readonly ITorrentEngine _torrentEngine;
    private readonly Torrent _torrent;
    private readonly string _torrentUrl;
    private readonly string _saveDirectory;
    private readonly Dictionary<int, int> _episodeToTorrentFileMap = new();
    private readonly ScheduledSubject<(double, ConnectionMonitor)> _downloadStatus = new(RxApp.MainThreadScheduler);
    private readonly CompositeDisposable _disposable = new();
    private static Stream _prevStream;
    private TorrentManager _torrentManager;
    private int _lastResolvedEp;

    public MonoTorrentStreamModelResolver(ITorrentEngine torrentEngine,
                                          IEnumerable<Element> parsedResults,
                                          string torrentUrl,
                                          string saveDirectory)
    {
        _torrentEngine = torrentEngine;
        _torrentUrl = torrentUrl;
        var folder = parsedResults.First(x => x.Category == Element.ElementCategory.ElementAnimeTitle).Value;
        _saveDirectory = Path.Combine(saveDirectory, folder);
    }

    public MonoTorrentStreamModelResolver(ITorrentEngine torrentEngine,
                                          Torrent torrent,
                                          string saveDirectory)
    {
        _torrentEngine = torrentEngine;
        _torrent = torrent;
        var parsedResults = AnitomySharp.AnitomySharp.Parse(torrent.Name);
        var folder = parsedResults.First(x => x.Category == Element.ElementCategory.ElementAnimeTitle).Value;
        _saveDirectory = Path.Combine(saveDirectory, folder);
    }

    public async ValueTask DisposeAsync()
    {
        _prevStream?.Dispose();
        _prevStream = null;
        _disposable.Dispose();
        await _torrentManager?.StopAsync();
        await _torrentEngine.SaveState();
    }

    public IObservable<(double,ConnectionMonitor)> Status => _downloadStatus;

    public async Task<EpisodeModelCollection> ResolveAllEpisodes(StreamType streamType)
    {
        _torrentManager = _torrent is null
            ? await _torrentEngine.DownloadFromUrl(_torrentUrl, _saveDirectory)
            : await _torrentEngine.Download(_torrent, _saveDirectory);

        if(_torrentManager is null)
        {
            return EpisodeModelCollection.Empty;
        }

        Observable
            .Timer(TimeSpan.Zero, TimeSpan.FromSeconds(3))
            .Select(_ => (_torrentManager.Progress, _torrentManager.Monitor))
            .Subscribe(_downloadStatus.OnNext)
            .DisposeWith(_disposable);

        var index = 0;
        foreach (var file in _torrentManager.Torrent.Files.Select(x => x.Path))
        {
            var result = AnitomySharp.AnitomySharp.Parse(file);
            if (result.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeNumber) is { } epResult &&
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

    public async Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, StreamType streamType)
    {
        _lastResolvedEp = episode;
        _prevStream?.Dispose();
        _prevStream = null;
        _prevStream = _torrent is null
            ? await _torrentEngine.GetStream(_torrentUrl, _episodeToTorrentFileMap[episode])
            : await _torrentEngine.GetStream(_torrent, _episodeToTorrentFileMap[episode]);

        return new VideoStreamsForEpisodeModel(_prevStream);
    }

    public void OnCompleted()
    {
        if(_torrentManager is null)
        {
            return;
        }

        if(_torrentManager.Torrent.Files.Count > 1)
        {
            return;
        }

        // if torrent contains multiple episodes, then only mark for delete when the final episode is completed.
        if(_lastResolvedEp < _episodeToTorrentFileMap.Keys.Max())
        {
            return;
        }

        _torrentEngine.MarkForDeletion(_torrentManager.InfoHash);
    }
}

