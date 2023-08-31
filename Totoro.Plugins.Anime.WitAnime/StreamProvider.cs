using System.Text.RegularExpressions;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Extractors;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.WitAnime;

internal partial class StreamProvider : IAnimeStreamProvider
{
    [GeneratedRegex(@"(\d+)")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"go_to_player\('(?<Url>.+)'\)")]
    private static partial Regex PlayerListRegex();

    public async Task<int> GetNumberOfStreams(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var lastNode = doc.QuerySelectorAll(".episodes-card").Last();
        return (int)double.Parse(NumberRegex().Match(lastNode.InnerText).Groups[1].Value);
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var total = 0;
        foreach (var item in doc.QuerySelectorAll(".anime-info"))
        {
            if (item.InnerText.Contains("عدد الحلقات:"))
            {
                total = int.Parse(NumberRegex().Match(item.InnerText).Groups[1].Value);
            }
        }

        if(total == 0)
        {
            yield break;
        }

        var (start, end) = episodeRange.Extract(total);

        foreach (var item in doc.QuerySelectorAll(".episodes-card"))
        {
            var titleNode = item.QuerySelector(".episodes-card-title a");
            var ep = int.Parse(NumberRegex().Match(titleNode.InnerHtml).Groups[1].Value);

            if(ep < start)
            {
                continue;
            }

            if(ep > end)
            {
                break;
            }

            var epUrl = titleNode.Attributes["href"].Value;
            var doc2 = await epUrl.GetHtmlDocumentAsync();

            foreach (var server in doc2.QuerySelectorAll("#episode-servers li a"))
            {
                var serverUrl = server.Attributes["data-ep-url"].Value;
                var serverName = server.InnerHtml;

                var stream = await GetStreams(serverUrl);

                if(stream is null)
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
        return serverUrl switch
        {
            string x when x.Contains("yonaplay") => await ExtractFromMultiUrl(serverUrl),
            string x when x.Contains("4shared") => await FourSharedExtractor.Extract(serverUrl),
            string x when x.Contains("soraplay") => await SoraPlayExtractor.Extract(serverUrl, Config.Url),
            string x when x.Contains("drive.google.com") => await GoogleDriveExtractor.Extract(serverUrl),
            //string x when x.Contains("dood") => await DoodExtractor.Extract(serverUrl, "Dood mirror"),
            //string x when x.Contains("mp4upload.com") => await Mp4UploadExtractor.Extract(serverUrl, Config.Url),
            _ => null
        };
    }

    private async Task<VideoStreamsForEpisode?> ExtractFromMultiUrl(string url)
    {
        var html = await url
            .WithReferer(Config.Url)
            .GetStringAsync();

        foreach (var match in PlayerListRegex().Matches(html).OfType<Match>())
        {
            var playerUrl = match.Groups["Url"].Value;
            var stream = await GetStreams(playerUrl);

            if(stream is null)
            {
                continue;
            }

            return stream;
        }

        return null;
    }
}
