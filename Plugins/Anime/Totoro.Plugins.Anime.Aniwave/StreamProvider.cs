using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Aniwave;

internal partial class StreamProvider : IAnimeStreamProvider
{
    [GeneratedRegex(@"(?<Start>\d+)-(?<End>\d+)")]
    private static partial Regex EpisodeRangeRegex();

    public async Task<int> GetNumberOfStreams(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var animeId = doc.QuerySelector("#watch-main").Attributes["data-id"].Value;
        var vrf = await Vrf.Encode(animeId);
        var body = await Config.Url
            .AppendPathSegment($"/ajax/episode/list/{animeId}")
            .SetQueryParam("vrf", vrf)
            .GetStringAsync();

        var jObject = JsonNode.Parse(body);
        var html = jObject!["result"]!.ToString();
        doc = new HtmlDocument();
        doc.LoadHtml(html);

        var epRange = doc.QuerySelector(".ep-range").Attributes["data-range"].Value;
        var match = EpisodeRangeRegex().Match(epRange);

        if (!match.Groups["End"].Success)
        {
            return 0;
        }

        return (int)double.Parse(match.Groups["End"].Value);
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var animeId = doc.QuerySelector("#watch-main").Attributes["data-id"].Value;
        var vrf = await Vrf.Encode(animeId);
        var body = await Config.Url
            .AppendPathSegment($"/ajax/episode/list/{animeId}")
            .SetQueryParam("vrf", vrf)
            .GetStringAsync();

        var jObject = JsonNode.Parse(body);
        var html = jObject!["result"]!.ToString();
        doc = new HtmlDocument();
        doc.LoadHtml(html);

        var epRange = doc.QuerySelector(".ep-range").Attributes["data-range"].Value;
        var match = EpisodeRangeRegex().Match(epRange);

        if (!match.Groups["End"].Success)
        {
            yield break;
        }

        var total = (int)double.Parse(match.Groups["End"].Value);
        var (start, end) = episodeRange.Extract(total);

        bool isSub = true;

        foreach (var item in doc.QuerySelectorAll("ul > li > a"))
        {
            var ids = item.Attributes["data-ids"].Value.Split(',');
            var id = isSub ? ids[0] : ids[1];
            
            if (!int.TryParse(item.Attributes["data-num"].Value, out int epNum))
            {
                continue;
            }

            if(epNum < start || epNum > end)
            {
                continue;
            }

            body = await Config.Url
                .AppendPathSegment($"/ajax/server/list/{id}")
                .SetQueryParam("vrf", await Vrf.Encode(id))
                .GetStringAsync();
            jObject = JsonNode.Parse(body);
            html = jObject!["result"]!.ToString();
            var doc2 = new HtmlDocument();
            doc2.LoadHtml(html);

            foreach (var serverItem in doc2.QuerySelectorAll("li"))
            {
                var name = serverItem.InnerText;
                var serverId = serverItem.Attributes["data-link-id"].Value;
                var result = await Config.Url
                    .AppendPathSegment($"/ajax/server/{serverId}")
                    .SetQueryParam("vrf", await Vrf.Encode(serverId))
                    .GetStringAsync();
                jObject = JsonNode.Parse(result);
                var encodedUrl = jObject!["result"]!["url"]!.ToString();
                var decoded = await Vrf.Decode(encodedUrl);

                if(await ExtractStreams(name, decoded) is not VideoStreamsForEpisode stream)
                {
                    continue;
                }

                yield return stream;
            }
        }

    }

    private Task<VideoStreamsForEpisode?> ExtractStreams(string serverName, string url)
    {
        return serverName switch
        {
            "Vidstream" or "MyCloud" => DefaultExtract(serverName, url),
            _ => Task.FromResult((VideoStreamsForEpisode?)null)
        };
    }

    private async Task<VideoStreamsForEpisode?> DefaultExtract(string serverName, string url)
    {
        var slug = new Url(url).PathSegments.Last();
        var isMyCloud = serverName == "MyCloud";
        var server = isMyCloud ? "Mcloud" : "Vizcloud";
        var apiResponse = await $"https://9anime.eltik.net/raw{server}?query={slug}&apikey=saikou".GetStringAsync();
        return null;
    }
}
