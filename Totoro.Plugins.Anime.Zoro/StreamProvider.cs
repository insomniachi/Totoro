using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Extractors;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Zoro;

internal partial class StreamProvider : IAnimeStreamProvider
{
    [GeneratedRegex(@"(/watch)?/(?<slug>[\w-]+-(?<id>\d+))")]
    private static partial Regex SlugIdRegex();
    private readonly Dictionary<int, string> _serverId = new()
    {
        {4, "rapidvideo" },
        {1, "rapidvideo" },
        {5, "streamsb" },
        {3, "streamtape" }
    };

    public async Task<int> GetNumberOfStreams(string url)
    {
        var match = SlugIdRegex().Match(url);

        if(!match.Success)
        {
            return 0;
        }

        var json = await Config.Ajax.AppendPathSegment($"/v2/episode/list/{match.Groups["id"].Value}")
            .WithHeader(HeaderNames.XRequestedWith, "XMLHttpRequest")
            .GetJsonAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(json.html);

        return doc.QuerySelectorAll(".ss-list a").Count;
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange)
    {
        var match = SlugIdRegex().Match(url);

        if (!match.Success)
        {
            yield break;
        }

        var json = await Config.Ajax.AppendPathSegment($"/v2/episode/list/{match.Groups["id"].Value}")
            .WithHeader(HeaderNames.XRequestedWith, "XMLHttpRequest")
            .GetJsonAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(json.html);
        var episodes = doc.QuerySelectorAll("a[title][data-number][data-id]");
        var (start, end) = episodeRange.Extract(episodes.Count);

        foreach (var item in episodes)
        {
            var ep = item.Attributes["data-number"].Value;
            var id = item.Attributes["data-id"].Value;
            var title = item.Attributes["title"].Value;
            if(!int.TryParse(ep, out int epInt))
            {
                continue;
            }

            if(!(epInt >= start && epInt<= end))
            {
                continue;
            }

            var stream = await ExtractEpisode(id);

            if(stream is null)
            {
                continue;
            }

            foreach (var vidStream in stream.Streams)
            {
                vidStream.Headers.Add(HeaderNames.Referer, Config.Url);
            }

            stream.Episode = epInt;
            stream.AdditionalInformation.Title = title;

            yield return stream;
        }
    }

    private async Task<VideoStreamsForEpisode?> ExtractEpisode(string dataId)
    {
        var json = await Config.Ajax.AppendPathSegment("/v2/episode/servers")
            .SetQueryParam("episodeId", dataId)
            .WithHeader(HeaderNames.XRequestedWith, "XMLHttpRequest")
            .GetJsonAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(json.html);

        foreach (var item in doc.QuerySelectorAll("div.server-item"))
        {
            var sourceId = item.Attributes["data-id"].Value;

            var sourceData = await Config.Ajax.AppendPathSegment("/v2/episode/sources")
                .SetQueryParam("id", sourceId)
                .GetJsonAsync();

            var type = (string)sourceData.type;
            var link = (string)sourceData.link;

            if (type != "iframe")
            {
                var stream = new VideoStreamsForEpisode();
                stream.Streams.Add(new VideoStream
                {
                    Resolution = "default",
                    Url = link
                });
                return stream;
            }

            var server = (int)sourceData.server;
            var cdn = _serverId.GetValueOrDefault(server, "unavailable");

            if (new[] { "streamsb", "streamtape", "unavailable" }.Contains(cdn))
            {
                return null;
            }

            return await RapidVideoExtractor.Extract(link);
        }

        return null;
    }
}
