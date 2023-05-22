namespace Totoro.Plugins.Anime.Contracts;

public interface IAiredAnimeEpisodeProvider
{
    IAsyncEnumerable<IAiredAnimeEpisode> GetRecentlyAiredEpisodes(int page = 1);
}

public interface IAiredAnimeEpisode
{
    string Title { get; }
    string Url { get; }
    string Image { get; }
    int Episode { get; }
    string EpisodeString { get; }
}