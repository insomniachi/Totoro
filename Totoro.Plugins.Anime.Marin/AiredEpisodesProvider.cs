using System.Text.Json.Nodes;
using System.Web;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Marin;

internal class AiredEpisodesProvider : IAiredAnimeEpisodeProvider
{
    class AiredEpisode : IAiredAnimeEpisode
    {
        required public string Title { get; set; }
        required public string Url { get; init; }
        required public string Image { get; init; }
        required public int Episode { get; init; }
        required public string EpisodeString { get; init; }
    }

    public async IAsyncEnumerable<IAiredAnimeEpisode> GetRecentlyAiredEpisodes(int page = 1)
    {
        var jar = Config.GetCookieJar();
        _ = await Config.Url.WithHeader(HeaderNames.Range, "bytes=0-0").WithCookies(jar).GetAsync();
        var token = HttpUtility.UrlDecode(jar.First(x => x.Name == "XSRF-TOKEN").Value);
        
        var releaseData = await Config.Url.AppendPathSegment("/episode")
            .WithCookies(jar)
            .WithHeader("x-inertia", true)
            .WithHeader("x-xsrf-token", token)
            .PostJsonAsync(new
            {
                eps_page = page,
                sort = "rel-d"
            })
            .ReceiveString();

        var jObject = JsonNode.Parse(releaseData);

        foreach (var item in jObject?["props"]?["episode_list"]?["data"]?.AsArray() ?? new JsonArray())
        {
            var epString = $"{item?["slug"]}";
            _ = int.TryParse(epString, out var epInt);
            yield return new AiredEpisode
            {
                EpisodeString = epString,
                Episode = epInt,
                Image = $"{item?["cover"]}",
                Title = $"{item?["anime"]?["title"]}",
                Url = Url.Combine(Config.Url, $"/anime/{item?["anime"]?["slug"]}"),
            };
        }

        yield break;
    }
}
