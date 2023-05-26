using Flurl;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.GogoAnime;

internal class Catalog : IAnimeCatalog
{
    class SearchResult : ICatalogItem, IHaveImage, IHaveYear
    {
        required public string Title { get; init; }
        required public string Url { get; init; }
        required public string Image { get; init; }
        required public string Year { get; init; }
    }

    public async IAsyncEnumerable<ICatalogItem> Search(string query)
    {
        var doc = await Config.Url.AppendPathSegment("/search.html")
                .SetQueryParam("keyword", query)
                .GetHtmlDocumentAsync();

        foreach (var item in doc.DocumentNode.QuerySelectorAll("ul.items li"))
        {
            var title = item.QuerySelector(".name a").Attributes["title"].Value;
            var image = item.QuerySelector("img").Attributes["src"].Value;
            var url = item.QuerySelector(".name a").Attributes["href"].Value;
            var year = item.QuerySelector(".released")?.InnerText?.Split(':')?.LastOrDefault()?.Trim() ?? string.Empty;

            yield return new SearchResult
            {
                Title = title,
                Url = Url.Combine(Config.Url, url),
                Image = image,
                Year = year
            };
        }
    }
}
