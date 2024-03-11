using System.Text.RegularExpressions;
using Flurl;
using HtmlAgilityPack.CssSelectors.NetCore;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.GogoAnime;

internal partial class AiredEpisodesProvider : IAiredAnimeEpisodeProvider
{
    public const string AJAX_URL = "https://ajax.gogocdn.net/ajax/page-recent-release.html";

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
        var doc = await AJAX_URL.SetQueryParams(new
        {
            page,
            type = 1
        }).GetHtmlDocumentAsync();

        var nodes = doc.QuerySelectorAll(".items li");
        foreach (var item in nodes)
        {
            var title = item.SelectSingleNode("div/a").Attributes["title"].Value;
            var path = item.SelectSingleNode("div/a").Attributes["href"].Value;
            var url = Url.Combine(ConfigManager<Config>.Current.Url, path);
            var img = item.SelectSingleNode("div/a/img").Attributes["src"].Value;
            var epString = EpisodeRegex().Match(url).Groups[1].Value;
            yield return new AiredEpisode
            {
                Title = title,
                Url = url,
                Image = img,
                Episode = int.Parse(epString),
                EpisodeString = epString
            };
        }
    }

    [GeneratedRegex("(\\d+)", RegexOptions.RightToLeft)]
    private static partial Regex EpisodeRegex();
}
