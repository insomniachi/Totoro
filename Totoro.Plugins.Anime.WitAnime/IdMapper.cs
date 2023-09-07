using Flurl.Http;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.WitAnime;

internal class IdMapper : IIdMapper
{
    public async Task<AnimeId> MapId(string url)
    {
        var html = await url.GetStringAsync();

        return new AnimeId
        {
            MyAnimeList = IdMappingHelper.GetIdFromHtml(html, RegexHelper.MyAnimeListLinkRegex())
        };
    }
}
