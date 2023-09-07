using System.Text.RegularExpressions;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.YugenAnime;

internal partial class IdMapper : IIdMapper
{
    [GeneratedRegex("\"mal_id\":(\\d+)")]
    private static partial Regex MalIdRegex();

    [GeneratedRegex(@"anilist.co/anime/(\d+)")]
    private static partial Regex AnilistIdRegex();

    public async Task<AnimeId> MapId(string url)
    {
        return new AnimeId
        {
            MyAnimeList = await IdMappingHelper.GetIdFromUrl(url, MalIdRegex()),
            AniList = await IdMappingHelper.GetIdFromUrl(url, AnilistIdRegex())
        };
    }
}
