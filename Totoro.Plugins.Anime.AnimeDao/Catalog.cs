using Flurl;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.AnimeDao;

internal class Catalog : IAnimeCatalog
{
    class SearchResult : ICatalogItem, IHaveRating, IHaveYear
    {
        required public string Image { get; init; }
        required public string Rating { get; init; }
        required public string Title { get; init; }
        required public string Url { get; init; }
        required public string Year { get; init; }
    }

    public async IAsyncEnumerable<ICatalogItem> Search(string query)
    {
        var doc = await Config.Url
            .AppendPathSegment("/search")
            .SetQueryParam("search", query)
            .GetHtmlDocumentAsync();

        foreach (var item in doc.QuerySelectorAll(".row .card-body"))
        {
            var title = item.QuerySelector(".animename").InnerHtml;
            var url = item.QuerySelector(".animeinfo a").Attributes["href"].Value;
            var image = item.QuerySelector(".animeposter img").Attributes["data-src"].Value;
            var rating = item.QuerySelector(".score").InnerText.Trim();
            var year = item.QuerySelector(".year").InnerText.Trim();

            yield return new SearchResult
            {
                Title = title,
                Url = Url.Combine(Config.Url, url),
                Image = image,
                Rating = rating,
                Year = year
            };
        }
    }
}
