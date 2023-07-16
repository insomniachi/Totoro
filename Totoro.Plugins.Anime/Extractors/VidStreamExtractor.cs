using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl.Http;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Extractors;

public static partial class VidStreamExtractor
{
    [GeneratedRegex(@"""file"": ?'([^()']*)'")]
    private static partial Regex FileRegex();

    public static async Task<VideoStreamsForEpisode?> Extract(string url)
    {
        if(url.Contains("srcd"))
        {
            var text = await url.GetStringAsync();
            var streamUrl = FileRegex().Match(text).Groups[1].Value;

            return new VideoStreamsForEpisode
            {
                Streams =
                {
                    new VideoStream
                    {
                        Url = streamUrl,
                        Resolution = "default",
                        Headers = 
                        { 
                            { HeaderNames.Referer, url } 
                        }
                    }
                },
            };

        }

        var doc = await url.GetHtmlDocumentAsync();
        var iframeUrl = doc.QuerySelector("iframe").Attributes["src"].Value;

        if(iframeUrl.Contains("filemoon"))
        {

        }

        return await GogoPlayExtractor.Extract(iframeUrl);
    }

}
