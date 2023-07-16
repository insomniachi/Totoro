using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Extractors;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.AnimeDao;

internal partial class StreamProvider : IAnimeStreamProvider
{
    [GeneratedRegex(@"(\d+)", RegexOptions.RightToLeft)]
    private partial Regex EpisodeRegex();

    [GeneratedRegex(@"""(/redirect/.*?)""")]
    private partial Regex RedirectRegex();

    public async Task<int> GetNumberOfStreams(string url)
    {
        var doc = await url.GetHtmlDocumentAsync();
        return doc.QuerySelectorAll(".episodelist").Count;
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange)
    {
        var doc = await url.GetHtmlDocumentAsync();
        var eps = doc.QuerySelectorAll(".episodelist");
        var count = eps.Count;
        var (start, end) = episodeRange.Extract(count);

        foreach (var item in eps)
        {
            var title = item.QuerySelector(".animename").InnerHtml;
            var match = EpisodeRegex().Match(title);
            
            if(!match.Success)
            {
                continue;
            }
            
            var episode = int.Parse(match.Groups[1].Value);
            if(episode < start || episode > end)
            {
                continue;
            }

            var epTitle = item.QuerySelector(".animetitle span")?.InnerHtml;
            var epUrl = Url.Combine(Config.Url, item.QuerySelector(".card-body a").Attributes["href"].Value);

            doc = await epUrl.GetHtmlDocumentAsync();
            var names = doc.QuerySelectorAll("#videotab .nav-link").Select(x => x.InnerText).ToList();  
            var matches = RedirectRegex().Matches(doc.Text);

            Url uri = string.Empty;
            var index = 0;
            foreach (var m in matches.OfType<Match>().Where(x => x.Success))
            {
                var redirUrl = Url.Combine(Config.Url, m.Groups[1].Value);
                IFlurlResponse result;
                try
                {
                    result = await redirUrl.HeadAsync();
                }
                catch
                {
                    index++;
                    continue;
                }

                uri = result.ResponseMessage.RequestMessage!.RequestUri!;
                var stream = await Extract(uri);
                if(stream is null)
                {
                    index++;
                    continue;
                }

                yield return stream;
                break;
            }

        }
    }

    private async Task<VideoStreamsForEpisode?> Extract(Url uri)
    {
        var host = uri.Host;
        if(host.Contains("vidstream"))
        {
            return await VidStreamExtractor.Extract(uri);
        }
        else if(host.Contains("sb"))
        {

        }
        else if(host.Contains("mixdrop"))
        {

        }
        else if(host.Contains("streamta"))
        {

        }

        return null;
    }
}
