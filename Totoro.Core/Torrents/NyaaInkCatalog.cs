using AnimDL.Core.Helpers;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;

namespace Totoro.Core.Torrents;

public class NyaaCatalog : ITorrentCatalog
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public NyaaCatalog(HttpClient httpClient,
                       ILocalSettingsService localSettingsService)
    {
        _httpClient = httpClient;
        _baseUrl = localSettingsService.ReadSetting("Nyaa", "https://nyaa.ink/").Wait();
    }

    public IAsyncEnumerable<TorrentModel> Recents() => Search("");

    public async IAsyncEnumerable<TorrentModel> Search(string query)
    {
        Stream stream;
        try
        {
            stream = await _httpClient.GetStreamAsync(_baseUrl, new Dictionary<string, string>
            {
                ["f"] = "2",
                ["c"] = "1_0",
                ["q"] = query,
                ["s"] = "seeders",
                ["o"] = "desc"
            });
        }
        catch
        {
            yield break;
        }

        var doc = new HtmlDocument();
        doc.Load(stream);
        var trimmedBaseUrl = _baseUrl.TrimEnd('/');
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
                CategoryImage = trimmedBaseUrl + image,
                Category = category,
                Name = name,
                Link = trimmedBaseUrl + link,
                MagnetLink = magnet,
                Size = size,
                Date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(date)).UtcDateTime.ToLocalTime(),
                Seeders = int.Parse(seeds),
                Leeches = int.Parse(leeches),
                Completed = int.Parse(completed),
            };
        }
    }
}
