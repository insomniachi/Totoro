using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

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
        var response = await Config.Url
            .AppendPathSegment("/index.php")
            .SetQueryParams(new
            {
                search = 1,
                key = query
            })
            .GetStringAsync();

        foreach (var item in JsonNode.Parse(response)?.AsArray() ?? new JsonArray())
        {
            var title = item!["name"]!.ToString();
            var image = item!["image"]!.ToString();
            var link = Url.Combine(Config.Url, "anime", item!["link"]!.ToString());

            yield return new SearchResult
            {
                Title = title,
                Image = image,
                Url = link
            };
        }
    }
}
