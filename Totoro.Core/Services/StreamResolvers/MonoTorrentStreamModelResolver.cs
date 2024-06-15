using Anitomy;
using MonoTorrent;
using MonoTorrent.Client;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.StreamResolvers;

public sealed class MonoTorrentStreamModelResolver : IVideoStreamModelResolver,
                                                     INotifyDownloadStatus,
                                                     ICompletionAware,
                                                     IAsyncDisposable,
                                                     ISpecialVideoStreamModelResolver
{
    private readonly ITorrentEngine _torrentEngine;
    private readonly ISettings _settings;
    private readonly Torrent _torrent;
    private readonly string _torrentUrl;
    private readonly string _saveDirectory;
    private readonly Dictionary<string, int> _episodeToTorrentFileMap = [];
    private readonly ScheduledSubject<(double, ConnectionMonitor)> _downloadStatus = new(RxApp.MainThreadScheduler);
    private readonly CompositeDisposable _disposable = [];
    private static Stream _prevStream;
    private TorrentManager _torrentManager;
    private int _lastResolvedEp;

    public MonoTorrentStreamModelResolver(ITorrentEngine torrentEngine,
                                          IEnumerable<Element> parsedResults,
                                          ISettings settings,
                                          string torrentUrl)
    {
        _torrentEngine = torrentEngine;
        _settings = settings;
        _torrentUrl = torrentUrl;
        var folder = parsedResults.First(x => x.Category == ElementCategory.AnimeTitle).Value;
        _saveDirectory = Path.Combine(settings.UserTorrentsDownloadDirectory, folder);
    }

    public MonoTorrentStreamModelResolver(ITorrentEngine torrentEngine,
                                          ISettings settings,
                                          Torrent torrent)
    {
        _torrentEngine = torrentEngine;
        _torrent = torrent;
        _settings = settings;
        var parsedResults = Anitomy.Anitomy.Parse(torrent.Name);
        var folder = parsedResults.First(x => x.Category == ElementCategory.AnimeTitle).Value;
        _saveDirectory = Path.Combine(settings.UserTorrentsDownloadDirectory, folder);
    }

    public async ValueTask DisposeAsync()
    {
        _prevStream?.Dispose();
        _prevStream = null;
        _disposable.Dispose();
        await _torrentEngine.SaveState();
        _ = _torrentManager?.StopAsync();
    }

    public IObservable<(double, ConnectionMonitor)> Status => _downloadStatus;

    public async Task<EpisodeModelCollection> ResolveAllEpisodes(StreamType streamType)
    {
        _torrentManager = _torrent is null
            ? await _torrentEngine.DownloadFromUrl(_torrentUrl, _saveDirectory)
            : await _torrentEngine.Download(_torrent, _saveDirectory);

        if (_torrentManager is null)
        {
            return EpisodeModelCollection.Empty;
        }

        Observable
            .Timer(TimeSpan.Zero, TimeSpan.FromSeconds(3))
            .Select(_ => (_torrentManager.Progress, _torrentManager.Monitor))
            .Subscribe(_downloadStatus.OnNext)
            .DisposeWith(_disposable);

        var index = 0;
        var eps = new EpisodeModelCollection(_settings.SkipFillers);
        foreach (var file in _torrentManager.Torrent.Files.Select(x => x.Path))
        {
            var result = Anitomy.Anitomy.Parse(file);
            if (result.FirstOrDefault(x => x.Category == ElementCategory.EpisodeNumber) is { } epResult)
            {
                var epModel = new EpisodeModel();
                _episodeToTorrentFileMap[epResult.Value] = index;
                if (int.TryParse(epResult.Value, out var ep))
                {
                    epModel.EpisodeNumber = ep;
                }
                else
                {
                    epModel.IsSpecial = true;
                    epModel.SpecialEpisodeNumber = epResult.Value;
                }

                eps.Add(epModel);
            }

            index++;
        }

        return eps;
    }

    public async Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, StreamType streamType)
    {
        _lastResolvedEp = episode;
        _prevStream?.Dispose();
        _prevStream = null;
        _prevStream = _torrent is null
            ? await _torrentEngine.GetStream(_torrentUrl, _episodeToTorrentFileMap[episode.ToString()])
            : await _torrentEngine.GetStream(_torrent, _episodeToTorrentFileMap[episode.ToString()]);

        return new VideoStreamsForEpisodeModel(_prevStream);
    }

    public async Task<VideoStreamsForEpisodeModel> ResolveSpecialEpisode(string episode, StreamType streamType)
    {
        _prevStream?.Dispose();
        _prevStream = null;
        _prevStream = _torrent is null
            ? await _torrentEngine.GetStream(_torrentUrl, _episodeToTorrentFileMap[episode.ToString()])
            : await _torrentEngine.GetStream(_torrent, _episodeToTorrentFileMap[episode.ToString()]);

        return new VideoStreamsForEpisodeModel(_prevStream);
    }

    public void OnCompleted()
    {
        if (_torrentManager is null)
        {
            return;
        }

        if (_torrentManager.Torrent.Files.Count > 1)
        {
            return;
        }

        // if torrent contains multiple episodes, then only mark for delete when the final episode is completed.
        if (_lastResolvedEp < _episodeToTorrentFileMap.Keys.Where(x => int.TryParse(x, out _)).Select(int.Parse).Max())
        {
            return;
        }

        _torrentEngine.MarkForDeletion(_torrentManager.InfoHash);
    }
}

