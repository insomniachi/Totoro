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


public interface IAnimeProvider
{
    IAsyncEnumerable<ICatalogItem> SearchAsync(string query);
    IAsyncEnumerable<VideoServer> GetServers(Uri uri, string episodeId);
    IAsyncEnumerable<Episode> GetEpisodes(string animeId);
}

public interface IVideoExtractor
{
    IAsyncEnumerable<VideoSource> Extract(Uri url);
}