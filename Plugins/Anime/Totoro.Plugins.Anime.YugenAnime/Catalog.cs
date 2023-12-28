using Flurl;
using HtmlAgilityPack.CssSelectors.NetCore;
using Splat;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.YugenAnime;

internal class Catalog : IAnimeCatalog, IEnableLogger
{
    class SearchResult : ICatalogItem, IHaveImage, IHaveSeason, IHaveRating
    {
        required public string Url { get; init; }
        required public string Title { get; init; }
        required public string Image { get; init; }
        required public string Year { get; init; }
        required public string Season { get; init; }
        required public string Rating { get; init; }
    }


    public async IAsyncEnumerable<ICatalogItem> Search(string query)
    {
        var doc = await ConfigManager<Config>.Current.Url.AppendPathSegment("discover")
            .SetQueryParam("q", query)
            .GetHtmlDocumentAsync();
        var nodes = doc.QuerySelectorAll(".anime-meta");

        if (nodes is null)
        {
            this.Log().Error("no results found");
            yield break;
        }

        foreach (var node in nodes)
        {
            var title = node.Attributes["title"].Value;
            var url = Url.Combine(ConfigManager<Config>.Current.Url, node.Attributes["href"].Value);
            var image = node.QuerySelector("img").Attributes["data-src"].Value;
            var season = node.QuerySelector(".anime-details span").InnerText.Split(" ");
            var rating = node.QuerySelector(".option")?.ChildNodes[1]?.InnerText?.Trim() ?? string.Empty;

            yield return new SearchResult
            {
                Url = url,
                Title = title,
                Image = image,
                Season = season[0],
                Rating = rating,
                Year = season.Length >= 2 ? season[1] : string.Empty,
            };
        }
    }
}
