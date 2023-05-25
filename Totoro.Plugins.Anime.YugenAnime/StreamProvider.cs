using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack.CssSelectors.NetCore;
using Splat;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.YugenAnime;

public partial class StreamProvider : IMultiLanguageAnimeStreamProvider, IAnimeStreamProvider, IEnableLogger
{
    public Task<int> GetNumberOfStreams(string url) => GetNumberOfStreams(url, Config.StreamType);

    public async Task<int> GetNumberOfStreams(string url, StreamType streamType)
    {
        var doc = await url.AppendPathSegment("watch").GetHtmlDocumentAsync();

        var episodeText = streamType == StreamType.EnglishSubbed
            ? "Episodes"
            : "Episodes (Dub)";

        var epSection = doc.QuerySelectorAll(".data")
                           .Select(x => x.InnerText)
                           .Where(x => x?.Contains(episodeText) == true)
                           .First();

        var match = EpisodeRegex().Match(epSection);

        if (!match.Success)
        {
            return 0;
        }

        return int.Parse(match.Groups[1].Value);
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range range, StreamType streamType)
    {
        var doc = await url.AppendPathSegment("watch").GetHtmlDocumentAsync();

        var epSection = doc.QuerySelectorAll(".data")
                           .Select(x => x.InnerText)
                           .Where(x => x?.Contains("Episodes") == true)
                           .First();

        var match = EpisodeRegex().Match(epSection);

        if (!match.Success)
        {
            yield break;
        }

        var totalEpisodes = int.Parse(match.Groups[1].Value);
        var (start, end) = range.Extract(totalEpisodes);

        var uri = new UriBuilder(url).Uri;
        var slug = uri.Segments[^1].TrimEnd('/');
        var number = uri.Segments[^2].TrimEnd('/');

        var baseUrl = Config.Url.TrimEnd('/');
        for (int ep = start; ep <= end; ep++)
        {
            var epUrl = $"{baseUrl}/watch/{number}/{slug}/{ep}/";
            doc = await epUrl.GetHtmlDocumentAsync();
            var hasDub = doc.DocumentNode.SelectSingleNode(@"/html/body/main/div/div/div[1]/div[2]/div[2]/div[2]/a")?.InnerText.Trim().StartsWith("Dub") == true;
            var streamKey = GetStreamKey(number, ep, streamType);

            var json = await Config.Url.AppendPathSegment("/api/embed/")
                .WithHeader(HeaderNames.XRequestedWith, "XMLHttpRequest")
                .PostUrlEncodedAsync(new
                {
                    id = GetStreamKey(number, ep, streamType),
                    ac = "0"
                })
                .ReceiveString();

            var jObject = JsonNode.Parse(json);

            var stream = new VideoStreamsForEpisode { Episode = ep };
            stream.StreamTypes.AddRange(hasDub ? new[] { StreamType.EnglishSubbed, StreamType.EnglishDubbed } : Enumerable.Empty<StreamType>());
            stream.Streams.Add(new VideoStream
            {
                Resolution = "default",
                Url = jObject!["hls"]!.AsArray()!.FirstOrDefault()!.ToString()
            });

            yield return stream;
        }
    }

    public IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range range) => GetStreams(url, range, Config.StreamType);

    private static string ToBase64String(string str) => Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
    private static string GetStreamKey(string id, int ep, StreamType streamType)
    {
        if(streamType == StreamType.EnglishSubbed)
        {
            return ToBase64String($"{id}|{ep}");
        }
        else
        {
            return ToBase64String($"{id}|{ep}|dub");
        }
    }

    [GeneratedRegex("(\\d+)")]
    private static partial Regex EpisodeRegex();
}
