using Flurl;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Zoro;

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
        var doc = await Config.Url.AppendPathSegment("recently-updated").SetQueryParam("page", page).GetHtmlDocumentAsync();

        foreach (var item in doc.QuerySelectorAll(".flw-item"))
        {
            var image = item.QuerySelector(".film-poster img").Attributes["data-src"].Value;
            var title = item.QuerySelector(".dynamic-name").InnerHtml;
            var url = item.QuerySelector(".dynamic-name").Attributes["href"].Value;
            var ep = item.QuerySelector(".tick-sub")?.InnerText?.Trim() ?? "";
            int.TryParse(ep, out var epInt);

            yield return new AiredEpisode
            {
                Title = title,
                Url = Url.Combine(Config.Url,url),
                Image = image,
                EpisodeString = ep,
                Episode = epInt
            };
        }
    }
}
