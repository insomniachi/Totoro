using System.Text.Json;
using System.Text.Json.Serialization;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Manga.Contracts;

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
        var response = await Config.Api.AppendPathSegment("/manga")
            .WithDefaultUserAgent()
            .SetQueryParam("title", query)
            .SetQueryParam("limit", 5)
            .SetQueryParam("includes[]", "cover_art")
            .GetJsonAsync();

        foreach (var item in response.data)
        {
            string title = item.attributes.title.en;
            string cover = string.Empty;

            try
            {
                foreach (var relation in item.relationships)
                {
                    if (relation.type != "cover_art")
                    {
                        continue;
                    }

                    cover = relation.attributes.fileName;
                }
            }
            catch { }

            yield return new SearchResult
            {
                Title = title,
                Url = item.id,
                Image = $"https://uploads.mangadex.org/covers/{item.id}/{cover}.256.jpg"
            };
        }
    }
}

