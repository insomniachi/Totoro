using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;
using Totoro.Plugins.Torrents.Contracts;
using Totoro.Plugins.Torrents.Models;

namespace Totoro.Plugins.Torrents.Nya;

internal class Tracker : ITorrentTracker
{

    public IAsyncEnumerable<TorrentModel> Recents() => Search(string.Empty);

    public async IAsyncEnumerable<TorrentModel> Search(string query)
    {
        HtmlDocument doc;
        if (ConfigManager<Config>.Current.SortBy is SortBy.Date)
        {
            doc = await ConfigManager<Config>.Current.Url.SetQueryParams(new
            {
                f = (int)ConfigManager<Config>.Current.Filter,
                c = ConfigManager<Config>.Current.Category.ToQueryParam(),
                q = query,
            }).GetHtmlDocumentAsync();
        }
        else
        {
            doc = await ConfigManager<Config>.Current.Url.SetQueryParams(new
            {
                f = (int)ConfigManager<Config>.Current.Filter,
                c = ConfigManager<Config>.Current.Category.ToQueryParam(),
                q = query,
                s = ConfigManager<Config>.Current.SortBy.ToString().ToLower(),
                o = ConfigManager<Config>.Current.SortDirection.ToQueryString()
            }).GetHtmlDocumentAsync();
        }

        foreach (var item in doc.QuerySelectorAll("table tr .success"))
        {
            var image = item.ChildNodes[1].ChildNodes[1].ChildNodes[1].Attributes["src"].Value;
            var category = item.ChildNodes[1].ChildNodes[1].Attributes["title"].Value;
            var name = item.ChildNodes[3].ChildNodes.Count <= 3
                ? item.ChildNodes[3].ChildNodes[1].InnerHtml
                : item.ChildNodes[3].ChildNodes[3].InnerHtml;
            var link = item.ChildNodes[5].ChildNodes[1].Attributes["href"].Value;
            var magnet = item.ChildNodes[5].ChildNodes[3].Attributes["href"].Value;
            var size = item.ChildNodes[7].InnerHtml;
            var date = item.ChildNodes[9].Attributes["data-timestamp"].Value;
            var seeds = item.ChildNodes[11].InnerHtml;
            var leeches = item.ChildNodes[13].InnerHtml;
            var completed = item.ChildNodes[15].InnerHtml;

            yield return new TorrentModel
            {
                CategoryImage = Url.Combine(ConfigManager<Config>.Current.Url, image),
                Category = category,
                Name = name,
                Link = Url.Combine(ConfigManager<Config>.Current.Url, link),
                Magnet = magnet,
                Size = size,
                Date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(date)).UtcDateTime.ToLocalTime(),
                Seeders = int.Parse(seeds),
                Leeches = int.Parse(leeches),
                Completed = int.Parse(completed),
            };
        }
    }
}
