using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.Contracts;

public interface IAnimeStreamProvider
{
    public IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange);
    public Task<int> GetNumberOfStreams(string url);
}

public interface IMultiLanguageAnimeStreamProvider
{
    public IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange, Language language);
    public Task<int> GetNumberOfStreams(string url, Language language);
}


public static class StreamProviderExtensions
{
    public static async Task<VideoStreamsForEpisode?> GetStream(this IAnimeStreamProvider provider, string url, int episode)
    {
        await foreach(var item in provider.GetStreams(url, episode..episode))
        {
            return item;
        }

        return null;
    }

    public static async Task<VideoStreamsForEpisode?> GetStream(this IMultiLanguageAnimeStreamProvider provider, string url, int episode, Language language)
    {
        await foreach (var item in provider.GetStreams(url, episode..episode, language))
        {
            return item;
        }

        return null;
    }
}