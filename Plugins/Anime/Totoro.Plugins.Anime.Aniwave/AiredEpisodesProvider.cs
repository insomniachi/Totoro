using System.ComponentModel.Design.Serialization;
using Flurl;
using Flurl.Http;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Plugins.Anime.Aniwave;

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
        var response = await Config.Url.AppendPathSegment("/ajax/home/widget/updated-all")
            .SetQueryParam("page", page)
            .GetJsonAsync();

        var html = response.result;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var item in doc.QuerySelectorAll(".item"))
        {
            var img = item.QuerySelector("img").Attributes["src"].Value;
            var title = item.QuerySelector(".name").InnerHtml;
            var epUrl = item.QuerySelector(".name").Attributes["href"].Value;
            var animeUrl = string.Join("/", epUrl.Split("/").SkipLast(1));
            var eps = item.QuerySelector(".sub").InnerText.Trim();
            _ = int.TryParse(eps, out int epInt);

            yield return new AiredEpisode
            {
                Title = title,
                Image = img,
                Url = Url.Combine(Config.Url, animeUrl),
                Episode = epInt,
                EpisodeString = eps
            };
        }

        yield break;
    }
}
