using Flurl;
using Flurl.Http;
using FlurlGraphQL.Querying;
using Newtonsoft.Json.Linq;
using Totoro.Plugins.Anime.AllAnime;
using Totoro.Plugins.Anime.Contracts;

namespace Plugin.AllAnime;

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
                }
            }
        }
        """;

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
            yield return new SearchResult
            {
                Title = $"{item?["name"]}",
                Url = Url.Combine(Config.Url, $"/anime/{item?["_id"]}"),
                Season = $"{item?["season"]?["quarter"]}",
                Year = $"{item?["season"]?["year"]}",
                Rating = $"{item?["score"]}",
                Image = $"{item?["thumbnail"]}"
            };
        }
    }
}
