using System.Text.RegularExpressions;
using AnimDL.Core.Helpers;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;

namespace Totoro.Core.Torrents;

internal partial class AnimeToshoCatalog : ITorrentCatalog
{
    private readonly string _baseUrl = @"https://mirror.animetosho.org/";
    private readonly HttpClient _httpClient;

    public AnimeToshoCatalog(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<TorrentModel> Recents()
    {
        var stream = await _httpClient.GetStreamAsync(_baseUrl, new Dictionary<string, string>
        {
            ["filter[0][t]"] = "nyaa_class",
            ["filter[0][v]"] = "trusted",
            ["order"] = ""
        });

        foreach (var item in Parse(stream))
        {
            yield return item;
        }
    }
    public async IAsyncEnumerable<TorrentModel> Search(string query)
    {
        var stream = await _httpClient.GetStreamAsync(_baseUrl.TrimEnd('/') + "/search" , new Dictionary<string, string>
        {
            ["q"] = query,
        });

        foreach (var item in Parse(stream))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<TorrentModel> SearchWithId(string query, long id)
    {
        var stream = await _httpClient.GetStreamAsync(_baseUrl.TrimEnd('/') + "/search", new Dictionary<string, string>
        {
            ["q"] = query,
            ["aids"] = id.ToString()
        });

        foreach (var item in Parse(stream))
        {
            yield return item;
        }
    }

    private static IEnumerable<TorrentModel> Parse(Stream stream)
    {
        var doc = new HtmlDocument();
        doc.Load(stream);

        foreach (var item in doc.QuerySelectorAll(".home_list_entry"))
        {
            var node = item.QuerySelector(".link a");
            var title = node.InnerHtml;
            var link = node.Attributes["href"].Value;
            var magnet = item.QuerySelector(".links").Descendants("a").First(x => x.InnerHtml.Contains("Magnet")).Attributes["href"].Value;
            var seedleech = item.QuerySelector(".links").SelectSingleNode("span[3]")?.Attributes["title"]?.Value;

            var model = new TorrentModel
            {
                Name = title,
                Link = link,
                MagnetLink = magnet,
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
