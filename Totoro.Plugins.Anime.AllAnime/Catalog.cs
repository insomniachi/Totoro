using Flurl;
using FlurlGraphQL.Querying;
using Newtonsoft.Json.Linq;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

namespace Totoro.Plugins.Anime.AllAnime;

public class Catalog : IAnimeCatalog
{
    public const string SEARCH_QUERY =
        $$"""
        query( $search: SearchInput
               $limit: Int
               $page: Int
               $translationType: VaildTranslationTypeEnumType
               $countryOrigin: VaildCountryOriginEnumType )
        {
            shows( search: $search
                    limit: $limit
                    page: $page
                    translationType: $translationType
                    countryOrigin: $countryOrigin )
            {
                pageInfo
                {
                    total
                }
                edges 
                {
                    _id,
                    name,
                    availableEpisodesDetail,
                    season,
                    score,
                    thumbnail,
                    malId,
                    aniListId
                }
            }
        }
        """;

    class SearchResult : ICatalogItem, IHaveImage, IHaveSeason, IHaveRating, IHaveMalId, IHaveAnilistId
    {
        required public string Season { get; init; }
        required public string Year { get; init; }
        required public string Image { get; init; }
        required public string Rating { get; init; }
        required public string Title { get; init; }
        required public string Url { get; init; }
        required public long MalId { get; init; }
        required public long AnilistId { get; init; }
    }

    public async IAsyncEnumerable<ICatalogItem> Search(string query)
    {
        var jObject = await Config.Api
            .WithGraphQLQuery(SEARCH_QUERY)
            .SetGraphQLVariables(new
            {
                search = new
                {
                    allowAdult = true,
                    allowUnknown = true,
                    query
                },
                limit = 40
            })
            .PostGraphQLQueryAsync()
            .ReceiveGraphQLRawJsonResponse();

        foreach (var item in jObject?["shows"]?["edges"] ?? new JArray())
        {
            var malId = long.Parse($"{item?["malId"]}");
            var aniListId = long.Parse($"{item?["aniListId"]}");

            yield return new SearchResult
            {
                Title = $"{item?["name"]}",
                Url = Url.Combine(Config.Url, $"/anime/{item?["_id"]}"),
                Season = $"{item?["season"]?["quarter"]}",
                Year = $"{item?["season"]?["year"]}",
                Rating = $"{item?["score"]}",
                Image = $"{item?["thumbnail"]}",
                MalId = malId,
                AnilistId = aniListId,
            };
        }
    }
}
