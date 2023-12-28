using System.Text;
using System.Text.RegularExpressions;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Extractors;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.WitAnime;

internal partial class StreamProvider : IAnimeStreamProvider
{
    [GeneratedRegex(@"(\d+)")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"go_to_player\('(?<Url>.+)'\)")]
    private static partial Regex PlayerListRegex();

    [GeneratedRegex(@"'([^']+)'")]
    public static partial Regex EpUrlRegex();

    private int GetNumberOfStreams(HtmlDocument doc)
    {
        var lastNode = doc.QuerySelectorAll(".episodes-card").Last();
        return (int)double.Parse(NumberRegex().Match(lastNode.InnerText).Groups[1].Value);
    }

    public async Task<int> GetNumberOfStreams(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        return GetNumberOfStreams(doc);
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var total = GetNumberOfStreams(doc);
        var (start, end) = episodeRange.Extract(total);

        foreach (var item in doc.QuerySelectorAll(".episodes-card"))
        {
            var titleNode = item.QuerySelector(".episodes-card-title a");
            var ep = int.Parse(NumberRegex().Match(titleNode.InnerHtml).Groups[1].Value);

            if (ep < start)
            {
                continue;
            }

            if (ep > end)
            {
                break;
            }

            var onClick = titleNode.Attributes["onclick"].Value;
            var epUrlEncoded = EpUrlRegex().Match(onClick).Groups[1].Value;
            var epUrl = Encoding.UTF8.GetString(Convert.FromBase64String(epUrlEncoded));
            var doc2 = await epUrl.GetHtmlDocumentAsync();

            foreach (var server in doc2.QuerySelectorAll("#episode-servers li a"))
            {
                var serverUrlEncoded = server.Attributes["data-url"].Value;
                var serverUrl = Encoding.UTF8.GetString(Convert.FromBase64String(serverUrlEncoded));

                var stream = await GetStreams(serverUrl);
                if (stream is null)
                {
                    continue;
                }

                stream.Episode = ep;
                yield return stream;
                break;
            }
        }
    }

    private async Task<VideoStreamsForEpisode?> GetStreams(string serverUrl)
    {
        try
        {
            return serverUrl switch
            {
                string x when x.Contains("yonaplay") => await ExtractFromMultiUrl(serverUrl),
                string x when x.Contains("4shared") => await FourSharedExtractor.Extract(serverUrl),
                string x when x.Contains("soraplay") => await SoraPlayExtractor.Extract(serverUrl, ConfigManager<Config>.Current.Url),
                string x when x.Contains("drive.google.com") => await GoogleDriveExtractor.Extract(serverUrl),
                string x when x.Contains("dailymotion") => await DailyMotionExtractor.Extract(serverUrl, ConfigManager<Config>.Current.Url),
                string x when x.Contains("ok.ru") => await OkRuExtractor.Extract(serverUrl),
                string x when x.Contains("dood") => await DoodExtractor.Extract(serverUrl, "Dood mirror"),
                string x when x.Contains("mp4upload.com") => await Mp4UploadExtractor.Extract(serverUrl, ConfigManager<Config>.Current.Url),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<VideoStreamsForEpisode?> ExtractFromMultiUrl(string url)
    {
        var html = await url
            .WithReferer(ConfigManager<Config>.Current.Url)
            .WithDefaultUserAgent()
            .GetStringAsync();

        foreach (var match in PlayerListRegex().Matches(html).OfType<Match>())
        {
            var playerUrl = match.Groups["Url"].Value;
            var stream = await GetStreams(playerUrl);

            if (stream is null)
            {
                continue;
            }

            return stream;
        }

        return null;
    }
}
