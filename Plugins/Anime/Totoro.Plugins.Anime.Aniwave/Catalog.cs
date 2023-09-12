using Flurl;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Aniwave;

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
        var vrf = await Vrf.Encode(query);
        var doc = await Config.Url
            .AppendPathSegment("/filter")
            .SetQueryParam("language[]", "subbed")
            .SetQueryParam("keyword", query)
            .SetQueryParam("vrf", vrf)
            .SetQueryParam("page", 1)
            .GetHtmlDocumentAsync();

        foreach (var item in doc.QuerySelectorAll("#list-items div.ani.poster.tip > a"))
        {
            var url = Url.Combine(Config.Url, item.Attributes["href"].Value);
            var imageNode = item.QuerySelector("img");
            var title = imageNode.Attributes["alt"].Value;
            var image = imageNode.Attributes["src"].Value;

            yield return new SearchResult
            {
                Image = image,
                Title = title,
                Url = url
            };
        }
    }
}
