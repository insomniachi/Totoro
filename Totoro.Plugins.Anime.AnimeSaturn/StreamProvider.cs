using System.Text.RegularExpressions;
using Flurl;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.AnimeSaturn;

internal partial class StreamProvider : IAnimeStreamProvider
{
    [GeneratedRegex(@"(\d+)")]
    private static partial Regex EpisodeNumberRegex();

    public async Task<int> GetNumberOfStreams(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var lastEpNode = doc.QuerySelectorAll(".episodes-button").LastOrDefault();

        if(lastEpNode is null)
        {
            return 0;
        }

        var text = lastEpNode.QuerySelector("a").InnerHtml;
        var match = EpisodeNumberRegex().Match(text);
        
        if(!match.Success)
        {
            return 0;
        }

        return int.Parse(match.Groups[1].Value);
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var epNodes = doc.QuerySelectorAll(".episodes-button");
        var lastEpNode = epNodes.LastOrDefault();

        if (lastEpNode is null)
        {
            yield break;
        }

        var total = GetEpNumber(lastEpNode);
        var (start, end) = episodeRange.Extract(total);

        foreach (var item in epNodes)
        {
            var ep = GetEpNumber(item);
            
            if(ep < start)
            {
                continue;
            }
            if(ep > end)
            {
                break;
            }

            var intermediatePageUrl = item.QuerySelector("a").Attributes["href"].Value;
            var intermediatePage = await intermediatePageUrl.GetHtmlDocumentAsync();
            var playerUrl = string.Empty;
            foreach (var link in intermediatePage.QuerySelectorAll("a"))
            {
                if(!link.InnerHtml.Contains(@"Guarda lo Streaming"))
                {
                    continue;
                }

                playerUrl = link.Attributes["href"].Value;
            }

            var playerPage = await playerUrl.GetHtmlDocumentAsync();
            var sourceElement = playerPage.QuerySelector("source");
            string stream;
            if(sourceElement is null)
            {
                playerPage = await playerUrl.SetQueryParam("s", "alt").GetHtmlDocumentAsync();
                sourceElement = playerPage.QuerySelector("source");
            }
            stream = sourceElement.Attributes["src"].Value;
            
            yield return new VideoStreamsForEpisode
            {
                Episode = ep,
                Streams =
                {
                    new VideoStream
                    {
                        Url = stream,
                        Resolution = "default"
                    }
                }
            };
        }

    }

    private int GetEpNumber(HtmlNode node)
    {
        var match = EpisodeNumberRegex().Match(node.QuerySelector("a").InnerHtml);

        if(!match.Success)
        {
            return 0;
        }

        return int.Parse(match.Groups[1].Value);
    }
}
