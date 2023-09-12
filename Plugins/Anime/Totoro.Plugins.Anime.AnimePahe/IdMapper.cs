using System.Text.RegularExpressions;
using Flurl.Http;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.AnimePahe;

internal partial class IdMapper : IIdMapper
{
    [GeneratedRegex(@"<meta name=""(?'Type'anidb|anilist|kitsu|myanimelist)"" content=""(?'Id'\d+)"">")]
    private static partial Regex IdRegex();

    public async Task<AnimeId> MapId(string url)
    {
        var html = await url.GetStringAsync();

        var animeId = new AnimeId();

        foreach (var match in IdRegex().Matches(html).OfType<Match>())
        {
            var id = long.Parse(match.Groups["Id"].Value);
            switch (match.Groups["Type"].Value)
            {
                case "anidb":
                    animeId.AniDb = id;
                    break;
                case "anilist":
                    animeId.AniList = id;
                    break;
                case "kitsu":
                    animeId.Kitsu = id;
                    break;
                case "myanimelist":
                    animeId.MyAnimeList = id;
                    break;
            }
        }

        return animeId;
    }
}
