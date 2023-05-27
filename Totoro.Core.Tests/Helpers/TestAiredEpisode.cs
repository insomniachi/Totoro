using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Tests.Helpers;

internal class TestAiredEpisode : IAiredAnimeEpisode
{
    public TestAiredEpisode()
    {
        Url = Random.Shared.Next().ToString();
    }

    public string Title { get; set; }
    public string Url { get; set; }
    public string Image { get; set; }
    public int Episode { get; set; }
    public string EpisodeString { get; set; }
}
