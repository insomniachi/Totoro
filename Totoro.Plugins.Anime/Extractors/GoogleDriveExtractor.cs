using System.Web;
using Flurl;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Extractors
{
    public static class GoogleDriveExtractor
    {
        public static async Task<VideoStreamsForEpisode> Extract(string url)
        {
            var id = new Url(url).PathSegments[^2];
            var doc = await $"https://drive.google.com/u/0/uc?id={id}&export=download".GetHtmlDocumentAsync();
            var streamUrl = HttpUtility.HtmlDecode(doc.QuerySelector("form").Attributes["action"].Value);

            return new VideoStreamsForEpisode
            {
                Streams =
                {
                    new VideoStream
                    {
                        Url = streamUrl
                    }
                }
            };
        }
    }
}
