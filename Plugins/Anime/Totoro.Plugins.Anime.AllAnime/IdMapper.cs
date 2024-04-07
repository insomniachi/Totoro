using FlurlGraphQL;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AllAnime;

internal class IdMapper : IIdMapper
{
    public async Task<AnimeId> MapId(string url)
    {
        var jObject = await ConfigManager<Config>.Current.Api
            .WithGraphQLQuery(StreamProvider.SHOW_QUERY)
            .SetGraphQLVariable("showId", url.Split('/').LastOrDefault()?.Trim())
            .PostGraphQLQueryAsync()
            .ReceiveGraphQLRawSystemTextJsonResponse();

        var id = new AnimeId();

        if (long.TryParse(jObject?["show"]?["malId"]?.ToString(), out long malId))
        {
            id.MyAnimeList = malId;
        }

        if (long.TryParse(jObject?["show"]?["aniListId"]?.ToString(), out long anilistId))
        {
            id.AniList = anilistId;
        }

        return id;
    }
}
