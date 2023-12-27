using System.Globalization;
using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AnimePahe;

internal class AiredEpisodesProvider : IAiredAnimeEpisodeProvider
{
    class AiredEpisode : IAiredAnimeEpisode, IHaveCreatedTime
    {
        required public DateTime CreatedAt { get; init; }
        required public string Title { get; set; }
        required public string Url { get; init; }
        required public string Image { get; init; }
        required public int Episode { get; init; }
        required public string EpisodeString { get; init; }
    }

    public async IAsyncEnumerable<IAiredAnimeEpisode> GetRecentlyAiredEpisodes(int page = 1)
    {
        var json = await ConfigManager<Config>.Current.Url.AppendPathSegment("api")
            .SetQueryParams(new
            {
                m = "airing",
                page
            })
            .GetStringAsync();

        var jObject = JsonNode.Parse(json);
        var data = jObject!["data"]!.AsArray();

        foreach (var item in data)
        {
            var title = $"{item!["anime_title"]}";
            var image = $"{item["snapshot"]}";
            var url = Url.Combine(ConfigManager<Config>.Current.Url, "anime", $"{item["anime_session"]}");
            var episode = (int)(double)item!["episode"]!.AsValue();
            var createdAt = DateTime.ParseExact($"{item["created_at"]}", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).ToLocalTime();

            yield return new AiredEpisode
            {
                Title = title,
                Image = image,
                Url = url,
                Episode = episode,
                EpisodeString = episode.ToString(),
                CreatedAt = createdAt
            };
        }
    }
}
