using System.Text.RegularExpressions;
using Flurl;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.YugenAnime;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.YugenAnime;

internal partial class AiredEpisodesProvider : IAiredAnimeEpisodeProvider
{

    class AiredEpisode : IAiredAnimeEpisode, IHaveCreatedTime
    {
        required public DateTime CreatedAt { get; set; }
        required public string Title { get; set; }
        required public string Url { get; init; }
        required public string Image { get; init; }
        required public int Episode { get; init; }
        required public string EpisodeString { get; init; }
    }

    public async IAsyncEnumerable<IAiredAnimeEpisode> GetRecentlyAiredEpisodes(int page = 1)
    {
        var doc = await Config.Url.AppendPathSegment("latest")
            .SetQueryParam("page", page.ToString())
            .GetHtmlDocumentAsync();

        var nodes = doc.QuerySelectorAll(".ep-card");

        foreach (var item in nodes)
        {
            var title = item.QuerySelector(".ep-origin-name").InnerText.Trim();
            var path = item.QuerySelector(".ep-thumbnail").Attributes["href"].Value;
            var url =  Url.Combine(Config.Url, path);
            var img = item.QuerySelector("img").Attributes["data-src"].Value;
            var time = item.QuerySelector("time").Attributes["datetime"].Value;
            var epString = EpisodeRegex().Match(url).Groups[1].Value;
            yield return new AiredEpisode
            {
                Title = title,
                Url = url,
                Image = img,
                CreatedAt = DateTime.Parse(time).ToLocalTime(),
                Episode = int.Parse(epString),
                EpisodeString = epString
            };
        }
    }

    [GeneratedRegex("(\\d+)", RegexOptions.RightToLeft)]
    private static partial Regex EpisodeRegex();
}
