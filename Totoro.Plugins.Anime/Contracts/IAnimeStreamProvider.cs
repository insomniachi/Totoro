using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.Contracts;

public interface IAnimeStreamProvider
{
    public IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange);
    public Task<int> GetNumberOfStreams(string url);
}

public interface IMultiLanguageAnimeStreamProvider
{
    public IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange, StreamType streamType);
    public Task<int> GetNumberOfStreams(string url, StreamType streamType);
}