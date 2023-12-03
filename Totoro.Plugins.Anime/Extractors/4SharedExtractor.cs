using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Extractors;

public class FourSharedExtractor
{
    public static async Task<VideoStreamsForEpisode?> Extract(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var sourceNode = doc.QuerySelector("source");

        if (sourceNode is null)
        {
            return null;
        }

        var streamUrl = sourceNode.Attributes["src"].Value;

        return new VideoStreamsForEpisode
        {
            Streams =
            {
                new VideoStream
                {
                    Url = streamUrl,
                    Headers =
                    {
                        { HeaderNames.UserAgent, FlurlExtensions.USER_AGENT }
                    }
                }
            }
        };
    }
}
