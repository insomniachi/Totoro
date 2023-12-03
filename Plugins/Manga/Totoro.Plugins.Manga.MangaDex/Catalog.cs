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
            string title = "";
            string cover = "";
            string id = "";

            try
            {
                title = item.attributes.title.en;
                cover = string.Empty;
                id = item.id;

                foreach (var relation in item.relationships)
                {
                    if (relation.type != "cover_art")
                    {
                        continue;
                    }

                    cover = $"https://uploads.mangadex.org/covers/{item.id}/{relation.attributes.fileName}.256.jpg";
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

