using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.AnimeSaturn
{
    internal partial class AiredEpisodesProvider : IAiredAnimeEpisodeProvider
    {
        [GeneratedRegex("-ep-\\d+")]
        private static partial Regex EpisodeRegex();

        class AiredEpisode : IAiredAnimeEpisode
        {
            required public string Title { get; set; }
            required public string Url { get; init; }
            required public string Image { get; init; }
            required public int Episode { get; init; }
            required public string EpisodeString { get; init; }
        }

        public async IAsyncEnumerable<IAiredAnimeEpisode> GetRecentlyAiredEpisodes(int page = 1)
        {
            var doc = await Config.Url
                .WithReferer(Config.Url)
                .WithHeader(HeaderNames.XRequestedWith, "XMLHttpRequest")
                .AppendPathSegment("fetch_pages.php")
                .SetQueryParam("request", "episodes")
                .PostUrlEncodedAsync(new
                {
                    page
                })
                .GetHtmlDocumentAsync();

            foreach (var item in doc.QuerySelectorAll(".anime-card"))
            {
                var episodeNode = item.QuerySelector(".anime-episode");
                var match = RegexHelper.IntegerRegex().Match(episodeNode.InnerHtml);
                var episode = (int)double.Parse(match.Groups[1].Value);
                var title = item.QuerySelector(".card-text a").Attributes["title"].Value;
                var epUrl = item.QuerySelector(".card-text a").Attributes["href"].Value;
                var lastSegment = new Url(epUrl).PathSegments.Last();
                var id = EpisodeRegex().Replace(lastSegment, "");
                var url = Url.Combine(Config.Url, $"/anime/{id}");
                var image = item.QuerySelector("img").Attributes["src"].Value;

                yield return new AiredEpisode
                {
                    Title = title,
                    Url = url,
                    Episode = episode,
                    EpisodeString = match.Groups[1].Value,
                    Image = image
                };
            }
                
        }
    }
}
