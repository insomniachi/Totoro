using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AnimeSaturn;

internal class Catalog : IAnimeCatalog
{
    class SearchResult : ICatalogItem, IHaveImage
    {
        required public string Title { get; init; }
        required public string Url { get; init; }
        required public string Image { get; init; }
    }

    public async IAsyncEnumerable<ICatalogItem> Search(string query)
    {
        var response = await ConfigManager<Config>.Current.Url
            .AppendPathSegment("/index.php")
            .SetQueryParams(new
            {
                search = 1,
                key = query
            })
            .GetStringAsync();

        foreach (var item in JsonNode.Parse(response)?.AsArray() ?? [])
        {
            var title = item!["name"]!.ToString();
            var image = item!["image"]!.ToString();
            var link = Url.Combine(ConfigManager<Config>.Current.Url, "anime", item!["link"]!.ToString());

            yield return new SearchResult
            {
                Title = title,
                Image = image,
                Url = link
            };
        }
    }
}
