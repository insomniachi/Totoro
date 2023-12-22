using System.Text.Json.Nodes;
using System.Web;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Marin;

internal class Catalog : IAnimeCatalog
{
    class SearchResult : ICatalogItem, IHaveImage, IHaveYear, IHaveType
    {
        public required string Title { get; init; }
        public required string Url { get; init; }
        public required string Image { get; init; }
        public required string Year { get; init; }
        public required string Type { get; init; }
    }

    public async IAsyncEnumerable<ICatalogItem> Search(string query)
    {
        var jar = Config.GetCookieJar();
        _ = await "https://marin.moe/".WithHeader(HeaderNames.Range, "bytes=0-0").WithCookies(jar).GetAsync();
        var token = HttpUtility.UrlDecode(jar.First(x => x.Name == "XSRF-TOKEN").Value);

        var json = await Config.Url.AppendPathSegment("/anime")
            .WithCookies(jar)
            .WithHeader("x-xsrf-token", token)
            .WithHeader("x-inertia", "true")
            .PostJsonAsync(new
            {
                search = query
            })
            .ReceiveString();

        var jObject = JsonNode.Parse(json);

        foreach (var item in jObject?["props"]?["anime_list"]?["data"]?.AsArray() ?? [])
        {
            yield return new SearchResult
            {
                Title = $"{item?["title"]}",
                Url = Url.Combine(Config.Url, $"/anime/{item?["slug"]}"),
                Image = $"{item?["cover"]}",
                Year = $"{item?["year"]}",
                Type = $"{item?["type"]?["name"]}"
            };
        }
    }
}
