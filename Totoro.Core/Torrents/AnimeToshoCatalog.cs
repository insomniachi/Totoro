using System.Text.RegularExpressions;
using AnimDL.Core.Helpers;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;

namespace Totoro.Core.Torrents;

public partial class AnimeToshoCatalog : ITorrentCatalog, ISubtitlesDownloader, IIndexedTorrentCatalog
{
    private readonly string _baseUrl = @"https://mirror.animetosho.org/";
    private readonly HttpClient _httpClient;
    private readonly string _subtitlesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Totoro", "ApplicationData", "TempSubtitles");
    private readonly string _zipFile;

    public AnimeToshoCatalog(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _zipFile = Path.Combine(_subtitlesFolder, "subtitles.7z");
        //Directory.CreateDirectory(_subtitlesFolder);
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

    public async IAsyncEnumerable<TorrentModel> Search(string query, AnimeId id)
    {
        var @params = new Dictionary<string, string>
        {
            ["q"] = query,
        };

        if(id.AniDb > 0)
        {
            @params["aids"] = id.AniDb.ToString();
        }

        var stream = await _httpClient.GetStreamAsync(_baseUrl.TrimEnd('/') + "/search", @params);

        foreach (var item in Parse(stream))
        {
            yield return item;
        }
    }

    public async Task<IEnumerable<KeyValuePair<string, string>>> DownloadSubtitles(string url)
    {
        if(File.Exists(_zipFile))
        {
            foreach (var item in Directory.EnumerateFileSystemEntries(_subtitlesFolder))
            {
                File.Delete(item);
            }
        }

        var zip = await GetSubtitles(url);

        if(string.IsNullOrEmpty(zip))
        {
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        using var s = await _httpClient.GetStreamAsync(zip);
        using var fs = new FileStream(_zipFile, FileMode.OpenOrCreate);
        try
        {
            await s.CopyToAsync(fs);
            fs.Dispose();
            var result = new List<KeyValuePair<string,string>>();
            using var archive = SevenZipArchive.Open(_zipFile);
            foreach (var item in archive.Entries.Where(x => x.Key.EndsWith(".ass") || x.Key.EndsWith(".srt") || x.Key.EndsWith(".vtt")))
            {
                result.Add(new(item.Key, Path.Combine(_subtitlesFolder, item.Key)));
                item.WriteToDirectory(_subtitlesFolder);
            }

            return result;
        }
        catch { }

        return Enumerable.Empty<KeyValuePair<string, string>>();
    }

    public async Task<string> GetSubtitles(string url)
    {
        var stream = await _httpClient.GetStreamAsync(url);
        var doc = new HtmlDocument();
        doc.Load(stream);

        foreach (var item in doc.DocumentNode.Descendants("a"))
        {
            if(item.InnerHtml?.Contains("All Attachments") == true)
            {
                return item.Attributes["href"].Value;
            }
        }

        return string.Empty;
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
            var torrentLink = item.QuerySelector(".links").Descendants("a").First(x => x.InnerHtml.Contains("Torrent")).Attributes["href"].Value;
            var magnet = item.QuerySelector(".links").Descendants("a").First(x => x.InnerHtml.Contains("Magnet")).Attributes["href"].Value;
            var seedleech = item.QuerySelector(".links").SelectSingleNode("span[3]")?.Attributes["title"]?.Value;

            var model = new TorrentModel
            {
                Name = title,
                Link = torrentLink,
                MagnetLink = magnet,
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
