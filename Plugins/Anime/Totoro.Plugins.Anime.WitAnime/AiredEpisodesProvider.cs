using System.Text;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.WitAnime;

internal class AiredEpisodesProvider : IAiredAnimeEpisodeProvider
{
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
        var doc = await Config.Url.AppendPathSegment($"episode/page/{page}").GetHtmlDocumentAsync();

        foreach (var item in doc.QuerySelectorAll(".anime-card-container"))
        {
            var titleNode = item.QuerySelector(".episodes-card-title a");
            var match = RegexHelper.IntegerRegex().Match(titleNode.InnerHtml);
            var ep = (int)double.Parse(match.Groups[1].Value);
            var animeNode = item.QuerySelector(".anime-card-title a");
            var redirect = animeNode.Attributes["onclick"].Value;
            var encoded = StreamProvider.EpUrlRegex().Match(redirect).Groups[1].Value;
            var url = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var title = animeNode.InnerHtml;
            var image = item.QuerySelector("img").Attributes["src"].Value;

            yield return new AiredEpisode
            {
                Episode = ep,
                EpisodeString = match.Groups[1].Value,
                Title = title,
                Url = url,
                Image = image
            };
        }
    }
}
