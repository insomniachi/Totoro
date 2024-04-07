using System.Text.Json.Nodes;
using Flurl;
using FlurlGraphQL;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AllAnime;

internal class Catalog : IAnimeCatalog
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
        var jObject = await ConfigManager<Config>.Current.Api
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
            .ReceiveGraphQLRawSystemTextJsonResponse();

        foreach (var item in jObject?["shows"]?["edges"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            _ = long.TryParse($"{item?["malId"]}", out long malId);
            _ = long.TryParse($"{item?["aniListId"]}", out long aniListId);
            var title = $"{item?["name"]}";
            var url = Url.Combine(ConfigManager<Config>.Current.Url, $"/anime/{item?["_id"]}");
            var season = "";
            var year = "";
            if(item?.ContainsKey(@"season") == true)
            {
                season = $"{item?["season"]?["quarter"]}";
                year = $"{item?["season"]?["year"]}";
            }
            var rating = $"{item?["score"]}";
            var image = $"{item?["thumbnail"]}";

            yield return new SearchResult
            {
                Title = title,
                Url = url,
                Season = season,
                Year = year,
                Rating = rating,
                Image = image,
                MalId = malId,
                AnilistId = aniListId,
            };
        }
    }
}
