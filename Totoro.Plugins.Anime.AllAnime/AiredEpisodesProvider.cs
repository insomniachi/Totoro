using Flurl;
using FlurlGraphQL.Querying;
using Newtonsoft.Json.Linq;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

namespace Totoro.Plugins.Anime.AllAnime;

internal class AiredEpisodesProvider : IAiredAnimeEpisodeProvider
{
    public const string SHOWS_QUERY =
        """
        query ($search: SearchInput,
               $page: Int,
               $limit: Int,
               $translationType: VaildTranslationTypeEnumType,
               $countryOrigin: VaildCountryOriginEnumType) {
            shows( search: $search
                   limit: $limit,
                   page: $page,
                   translationType: $translationType,
                   countryOrigin: $countryOrigin ) {
                edges {
                    _id,
                    name,
                    thumbnail,
                    lastEpisodeDate,
                    lastEpisodeInfo,
                    malId,
                    aniListId
                }  
            }
        }
        """;

    class AiredEpisode : IAiredAnimeEpisode, IHaveCreatedTime, IHaveAnilistId, IHaveMalId
    {
        required public DateTime CreatedAt { get; init; }
        required public string Title { get; set; }
        required public string Url { get; init; }
        required public string Image { get; init; }
        required public int Episode { get; init; }
        required public string EpisodeString { get; init; }
        required public long MalId { get; init; }
        required public long AnilistId { get; init; }
    }

    public async IAsyncEnumerable<IAiredAnimeEpisode> GetRecentlyAiredEpisodes(int page = 1)
    {
        var jObject = await Config.Api
            .WithGraphQLQuery(SHOWS_QUERY)
            .SetGraphQLVariables(new
            {
                search = new
                {
                    allowAdult = false,
                    allowUnknown = false,
                },
                limit = 26,
                page,
                translationType = Config.StreamType.ConvertToTranslationType(),
                countryOrigin = Config.CountryOfOrigin
            })
            .PostGraphQLQueryAsync()
            .ReceiveGraphQLRawJsonResponse();

        foreach (var item in jObject?["shows"]?["edges"] ?? new JArray())
        {
            var title = $"{item?["name"]}";
            var image = $"{item?["thumbnail"]}";
            var year = int.Parse($"{item?["lastEpisodeDate"]?["sub"]?["year"]}");
            var month = int.Parse($"{item?["lastEpisodeDate"]?["sub"]?["month"]}") + 1; // months are from 0-11
            var day = int.Parse($"{item?["lastEpisodeDate"]?["sub"]?["date"]}");
            var hour = int.Parse($"{item?["lastEpisodeDate"]?["sub"]?["hour"]}");
            var min = int.Parse($"{item?["lastEpisodeDate"]?["sub"]?["minute"]}");
            var ep = $"{item?["lastEpisodeInfo"]?["sub"]?["episodeString"]}";
            _ = int.TryParse(ep, out int epInt);
            var datetime = new DateTime(year, month, day, hour, min, 0).ToLocalTime();
            var animeUrl = Url.Combine(Config.Url, $"/anime/{item?["_id"]}");
            var malId = $"{item?["malId"]}";
            var aniListId = $"{item?["aniListId"]}";

            yield return new AiredEpisode
            {
                Title = title,
                Image = image,
                CreatedAt = datetime,
                Episode = epInt,
                EpisodeString = ep,
                Url = animeUrl,
                MalId = string.IsNullOrEmpty(malId) ? 0 : long.Parse(malId),
                AnilistId = string.IsNullOrEmpty(aniListId) ? 0 : long.Parse(aniListId),
            };
        }
    }
}
