using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Manga.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Manga.MangaDex;

#nullable disable

internal class Catalog : IMangaCatalog
{
    class SearchResult : ICatalogItem
    {
        required public string Title { get; init; }
        required public string Url { get; init; }
        required public string Image { get; init; }
    }

    public async IAsyncEnumerable<ICatalogItem> Search(string query)
    {
        var response = await ConfigManager<Config>.Current.Api.AppendPathSegment("/manga")
            .WithDefaultUserAgent()
            .SetQueryParam("title", query)
            .SetQueryParam("limit", 5)
            .SetQueryParam("includes[]", "cover_art")
            .GetStreamAsync();

        var jObject = JsonNode.Parse(response);

        foreach (var item in jObject?[@"data"]?.AsArray() ?? [])
        {
            string title = "";
            string cover = "";
            string id = "";

            try
            {
                title = item?["attributes"]?["title"]?["en"]?.ToString();
                cover = string.Empty;
                id = item?["id"]?.ToString();

                foreach (var relation in item?["relationships"]?.AsArray() ?? [])
                {
                    if (relation?["type"]?.ToString() != "cover_art")
                    {
                        continue;
                    }

                    cover = $"https://uploads.mangadex.org/covers/{id}/{relation?["attributes"]?["fileName"]}.256.jpg";
                }
            }
            catch
            {
                continue;
            }

            yield return new SearchResult
            {
                Title = title,
                Url = id,
                Image = cover
            };
        }
    }
}

