using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Torrents.Contracts;
using Totoro.Plugins.Torrents.Models;

namespace Totoro.Plugins.Torrents.AnimeTosho;

internal partial class Tracker : ITorrentTracker
{
    public async IAsyncEnumerable<TorrentModel> Recents()
    {
        var doc = await Config.Url
            .SetQueryParam("filter[0][t]", "nyaa_class")
            .SetQueryParam("filter[0][v]", Config.Filter.ToQueryParamter())
            .SetQueryParam("order", Config.Sort.ToQueryParamter())
            .GetHtmlDocumentAsync();

        foreach (var item in Parse(doc))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<TorrentModel> Search(string query)
    {
        var doc = await Config.Url
            .AppendPathSegment("search")
            .SetQueryParam("q", query)
            .SetQueryParam("filter[0][t]", "nyaa_class")
            .SetQueryParam("filter[0][v]", Config.Filter.ToQueryParamter())
            .SetQueryParam("order", Config.Sort.ToQueryParamter())
            .GetHtmlDocumentAsync();

        foreach (var item in Parse(doc))
        {
            yield return item;
        }
    }

    private static IEnumerable<TorrentModel> Parse(HtmlDocument doc)
    {
        foreach (var item in doc.QuerySelectorAll(".home_list_entry"))
        {
            var node = item.QuerySelector(".link a");
            var title = node.InnerHtml;
            var link = node.Attributes["href"].Value;
            var torrentLink = item.QuerySelector(".links").Descendants("a").First(x => x.InnerHtml.Contains("Torrent")).Attributes["href"].Value;
            var magnet = item.QuerySelector(".links").Descendants("a").First(x => x.InnerHtml.Contains("Magnet")).Attributes["href"].Value;
            var seedleech = item.QuerySelector(".links").SelectSingleNode("span[3]")?.Attributes["title"]?.Value;

            var model = new TorrentModel
            {
                Name = title,
                Link = torrentLink,
                Magnet = magnet,
                Link2 = link
            };

            if (seedleech is not null)
            {
                var match = SeedLeechRegex().Match(seedleech);
                var seed = int.Parse(match.Groups[1].Value);
                var leech = int.Parse(match.Groups[2].Value);
                var size = item.QuerySelector(".size").InnerHtml;

                model.Seeders = seed;
                model.Leeches = leech;
                model.Size = size;
            }

            yield return model;
        }
    }

    [GeneratedRegex("Seeders: (\\d+) / Leechers: (\\d)+")]
    private static partial Regex SeedLeechRegex();
}
