using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

namespace Totoro.Plugins.Anime.Zoro;

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
        var json = await Config.Ajax.AppendPathSegment("/search/suggest").SetQueryParam("keyword", query).GetJsonAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(json.html);

        foreach (var item in doc.QuerySelectorAll("a").SkipLast(1))
        {
            var image = item.QuerySelector(".film-poster img").Attributes["data-src"].Value;
            var title = item.QuerySelector(".srp-detail h3").InnerHtml;
            var url = item.Attributes["href"].Value;

            yield return new SearchResult
            {
                Title = title,
                Url = new Url(Url.Combine(Config.Url, url)).RemoveQuery().ToString(),
                Image = image
            };
        }

        yield break;
    }
}
