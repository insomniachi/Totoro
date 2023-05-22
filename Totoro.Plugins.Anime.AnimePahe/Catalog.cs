using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;
using Splat;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Plugins.Anime.AnimePahe;

public class Catalog : IAnimeCatalog, IEnableLogger
{
    public async IAsyncEnumerable<ICatalogItem> Search(string query)
    {
        var json = await Config.Url
            .AppendPathSegment("api")
            .SetQueryParams(new
            {
                m = "search",
                q = query,
                l = 8
            })
            .GetStringAsync();


        if (string.IsNullOrEmpty(json))
        {
            this.Log().Error("did not get a response");
            yield break;
        }

        var jObject = JsonNode.Parse(json);

        if (jObject is null)
        {
            this.Log().Error("unable to parse {Json}", json);
            yield break;
        }

        if (jObject["data"]?.AsArray() is not { } results)
        {
            this.Log().Error("there is not data");
            yield break;
        }

        var baseAnimeUrl = new Url(Config.Url).AppendPathSegment("anime");
        foreach (var item in results)
        {
            yield return new SearchResult
            {
                Title = $"{item?["title"]}",
                Image = $"{item?["poster"]}",
                Url = Url.Combine(baseAnimeUrl, $"{item?["session"]}"),
                Status = $"{item?["status"]}",
                Season = $"{item?["season"]}",
                Year = $"{item?["year"]}"
            };
        }

    }
}