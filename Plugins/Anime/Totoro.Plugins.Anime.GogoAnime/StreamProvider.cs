using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack.CssSelectors.NetCore;
using Splat;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.GogoAnime;

internal partial class StreamProvider : IAnimeStreamProvider, IEnableLogger
{
    public const string EPISODE_LOAD_AJAX = "https://ajax.gogo-load.com/ajax/load-list-episode";

    [GeneratedRegex("<input.*?value=\"([0-9]+)\".*?id=\"movie_id\"", RegexOptions.Compiled)]
    private static partial Regex AnimeIdRegex();


    public async Task<int> GetNumberOfStreams(string url)
    {
        var html = await ConvertHost(url).GetStringAsync();

        var match = AnimeIdRegex().Match(html);

        if (!match.Success)
        {
            this.Log().Error("unable to match id regex");
            return 0;
        }

        var contentId = match.Groups[1].Value;

        html = await EPISODE_LOAD_AJAX
            .SetQueryParams(new
            {
                ep_start = "0",
                ep_end = "100000",
                id = contentId
            })
            .GetStringAsync();

        var epMatch = EpisodeRegex().Match(html);

        if (!epMatch.Success)
        {
            return 0;
        }

        return int.Parse(epMatch.Groups[1].Value);
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range range)
    {
        var html = await ConvertHost(url).GetStringAsync();

        var match = AnimeIdRegex().Match(html);

        if (!match.Success)
        {
            this.Log().Error("unable to match id regex");
            yield break;
        }

        var contentId = match.Groups[1].Value;

        html = await EPISODE_LOAD_AJAX
            .SetQueryParams(new
            {
                ep_start = "0",
                ep_end = "100000",
                id = contentId
            })
            .GetStringAsync();

        var epMatch = EpisodeRegex().Match(html);

        int start = 0;
        int end = 100000;
        if (!epMatch.Success)
        {
            this.Log().Error("did not find any episode number");
        }
        else
        {
            var count = int.Parse(epMatch.Groups[1].Value);
            (start, end) = range.Extract(count);
        }

        await foreach (var (ep, embedUrl) in GetEpisodeList(contentId, start, end))
        {
            var embedPageUrl = await GetEmbedPage(embedUrl);
            var stream = await GogoPlayExtractor.Extract(embedPageUrl);

            if (stream is null)
            {
                this.Log().Error("unable to find stream {Url}", embedUrl);
                continue;
            }

            stream.Episode = ep;
            yield return stream;
        }
    }

    private async IAsyncEnumerable<(int ep, string embedUrl)> GetEpisodeList(string contentId, int start, int end)
    {
        var doc = await EPISODE_LOAD_AJAX
            .SetQueryParams(new
            {
                ep_start = start,
                ep_end = end,
                id = contentId
            })
            .GetHtmlDocumentAsync();

        foreach (var item in doc.QuerySelectorAll("a[class=\"\"] , a[class=\"\"]").Reverse())
        {
            var path = item.Attributes["href"].Value.Trim();
            var embedUrl = Url.Combine(ConfigManager<Config>.Current.Url, path);
            var epMatch = EpisodeRegex().Match(item.InnerHtml);
            int ep = -1;

            if (epMatch.Success)
            {
                ep = int.Parse(epMatch.Groups[1].Value);
            }
            else
            {
                this.Log().Error("unable to match episode number");
            }

            yield return (ep, embedUrl);
        }
    }

    private static async Task<string> GetEmbedPage(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        return $"{doc.QuerySelector("iframe").Attributes["src"].Value}";
    }

    private static string ConvertHost(string url)
    {
        var converted = UrlRegex().Replace(url, ConfigManager<Config>.Current.Url);
        return converted;
    }

    [GeneratedRegex("EP.*?(\\d+)")]
    private static partial Regex EpisodeRegex();

    [GeneratedRegex("https?://gogoanime.\\w*/")]
    private static partial Regex UrlRegex();
}
