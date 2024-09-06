using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Extractors;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Anime4Up;

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

            var epUrl = titleNode.Attributes["href"].Value;
            var doc2 = await epUrl.GetHtmlDocumentAsync();
            var b64json = doc2.QuerySelector("input[name=wl]").Attributes["Value"].Value;
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64json));
            var obj = JsonNode.Parse(json)!.AsObject();

            IReadOnlyList<string> qualities = ["fhd", "hd", "sd"];
            foreach (var quality in qualities)
            {
                if (await GetStreams(quality, obj, ep) is not { } stream)
                {
                    continue;
                }

                foreach (var s in stream.Streams)
                {
                    s.Headers.Add(HeaderNames.Referer, ConfigManager<Config>.Current.Url);
                }

                yield return stream;
            }
        }

        yield break;
    }

    public async Task<VideoStreamsForEpisode?> GetStreams(string quality, JsonObject qualities, int episode)
    {
        if (!qualities.ContainsKey(quality))
        {
            return null;
        }

        foreach (var link in qualities[quality]!.AsObject())
        {
            var stream = await GetStreams(link.Value!.GetValue<string>());
            if (stream is null)
            {
                continue;
            }
            stream.Episode = episode;
            return stream;
        }

        return null;
    }

    private async Task<VideoStreamsForEpisode?> GetStreams(string serverUrl)
    {
        try
        {
            return serverUrl switch
            {
                string x when x.Contains("4shared") => await FourSharedExtractor.Extract(serverUrl),
                string x when x.Contains("soraplay") => await SoraPlayExtractor.Extract(serverUrl, ConfigManager<Config>.Current.Url),
                string x when x.Contains("drive.google.com") => await GoogleDriveExtractor.Extract(serverUrl),
                string x when x.Contains("dailymotion") => await DailyMotionExtractor.Extract(serverUrl, ConfigManager<Config>.Current.Url),
                string x when x.Contains("ok.ru") => await OkRuExtractor.Extract(serverUrl),
                string x when x.Contains("dood") => await DoodExtractor.Extract(serverUrl, "Dood mirror"),
                string x when x.Contains("mp4upload.com") => await Mp4UploadExtractor.Extract(serverUrl, ConfigManager<Config>.Current.Url),
                string x when x.Contains("sega") => await VidBomExtractor.Extract(serverUrl),
                string x when x.Contains("uqload") => await UqLoadExtractor.Extract(serverUrl),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
