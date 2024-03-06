using System.Data;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Aniwave;

internal partial class StreamProvider : IAnimeStreamProvider
{
    [GeneratedRegex(@"(?<Start>\d+)-(?<End>\d+)")]
    private static partial Regex EpisodeRangeRegex();

    [GeneratedRegex(@"var k='(?<k>[^']+)'")]
    private static partial Regex FuTokenRegex();

    private readonly string _vidSrcKeys = "https://raw.githubusercontent.com/KillerDogeEmpire/vidplay-keys/keys/keys.json";

    public async Task<int> GetNumberOfStreams(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var animeId = doc.QuerySelector("#watch-main").Attributes["data-id"].Value;
        var vrf = await Vrf.Encode(animeId);
        var body = await ConfigManager<Config>.Current.Url
            .WithDefaultUserAgent()
            .WithReferer(url)
            .WithHeader(HeaderNames.XRequestedWith, "XMLHttpRequest")
            .AppendPathSegment($"/ajax/episode/list/{animeId}")
            .SetQueryParam("vrf", $"{vrf}#{url}")
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
        var body = await ConfigManager<Config>.Current.Url
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

            if (epNum < start || epNum > end)
            {
                continue;
            }

            body = await ConfigManager<Config>.Current.Url
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
                var result = await ConfigManager<Config>.Current.Url
                    .AppendPathSegment($"/ajax/server/{serverId}")
                    .SetQueryParam("vrf", await Vrf.Encode(serverId))
                    .GetStringAsync();

                jObject = JsonNode.Parse(result);
                var encodedUrl = jObject!["result"]!["url"]!.ToString();
                var decoded = await Vrf.Decode(encodedUrl);

                if (await ExtractStreams(name, decoded) is not VideoStreamsForEpisode stream)
                {
                    continue;
                }
                stream.Episode = epNum;
                yield return stream;
            }
        }

    }

    private async Task<VideoStreamsForEpisode?> ExtractStreams(string serverName, string url)
    {
        return serverName switch
        {
            "Vidplay" or "mycloud" => await DefaultExtract(serverName, url),
            _ => null
        };
    }

    private static async Task<string> GetApiUrl(string url, List<string> keys)
    {
        Url urlObj = url;
        var host = urlObj.Host;
        var vidId = urlObj.QueryParams[0].Value.ToString()!;
        var @params = urlObj.QueryParams.Select(x => (x.Name, x.Value.ToString()!));
        var encodedId = GetEncodedId(vidId, keys);
        var apiSlug = await CallFuToken(host, encodedId);
        return BuildUrl(host, apiSlug, @params);
    }

    private static string GetEncodedId(string vidId, List<string> keys)
    {
        var encoded = RC42.Decrypt(Encoding.UTF8.GetBytes(keys[0]), Encoding.UTF8.GetBytes(vidId));
        encoded = RC42.Decrypt(Encoding.UTF8.GetBytes(keys[1]), encoded);
        var bytes = Encoding.UTF8.GetBytes(Convert.ToBase64String(encoded));
        var str =  Encoding.UTF8.GetString(bytes);
        return str.Replace("/", "_").Trim();
    }

    static string BuildUrl(string host, string apiSlug, IEnumerable<(string, string)> parameters)
    {
        StringBuilder urlBuilder = new StringBuilder();
        urlBuilder.Append("https://");
        urlBuilder.Append(host);
        urlBuilder.Append('/');
        urlBuilder.Append(apiSlug);

        if (parameters.Any())
        {
            urlBuilder.Append('?');
            urlBuilder.Append(string.Join("&", parameters.Select(p => $"{p.Item1}={p.Item2}")));
        }

        return urlBuilder.ToString();
    }

    private static async Task<string> CallFuToken(string host, string data)
    {
        var fuTokenScript = await $"https://{host}/futoken".WithDefaultUserAgent().GetStringAsync();
        var match = FuTokenRegex().Match(fuTokenScript);
        if(!match.Success)
        {
            return "";
        }    

        string v = data;
        string k = match.Groups["k"].Value;
        var codes = new List<int>();
        for (int i = 0; i < v.Length; i++)
        {
            int charCode = k[i % k.Length] + v[i];
            codes.Append(charCode);
        }

        return $"mediainfo/{k},{string.Join(",", codes)}";
    }



    private async Task<VideoStreamsForEpisode?> DefaultExtract(string serverName, string url)
    {
        var host = new Url(url).Host;
        var keys = await _vidSrcKeys.GetJsonAsync<List<string>>();
        var apiUrl = await GetApiUrl(url, keys);

        var result = await apiUrl.WithReferer(HttpUtility.UrlDecode(url))
                                 .WithHeader(HeaderNames.XRequestedWith, "XMLHttpRequest")
                                 .WithHeader(HeaderNames.Host, host)
                                 .GetStringAsync();

        return new VideoStreamsForEpisode
        {
            Streams =
            {
                new VideoStream
                {
                    Url = "",
                    Headers = { { HeaderNames.Referer, url } }
                }
            }
        };
    }
}
