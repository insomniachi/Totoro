using LanguageExt.Common;
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


public static class StreamProviderExtensions
{
    public static async Task<Result<VideoStreamsForEpisode>> GetStream(this IAnimeStreamProvider provider, string url, int episode)
    {
        await foreach (var item in provider.GetStreams(url, episode..episode))
        {
            return item;
        }

        return new(new Exception("Episode not found"));
    }

    public static async Task<Result<VideoStreamsForEpisode>> GetStream(this IMultiLanguageAnimeStreamProvider provider, string url, int episode, StreamType streamType)
    {
        await foreach (var item in provider.GetStreams(url, episode..episode, streamType))
        {
            return item;
        }

        return new(new Exception("Episode not found"));
    }
}