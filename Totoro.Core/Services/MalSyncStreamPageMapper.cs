using System.Diagnostics;
using System.Text.Json.Nodes;
using AnimDL.Api;

namespace Totoro.Core.Services
{
    public class MalSyncStreamPageMapper : IStreamPageMapper
    {
        private readonly HttpClient _httpClient;

        public MalSyncStreamPageMapper(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<(SearchResult Sub, SearchResult Dub)?> GetStreamPage(long malId, ProviderType provider)
        {
            var json = await _httpClient.GetStringAsync($"https://raw.githubusercontent.com/MALSync/MAL-Sync-Backup/master/data/myanimelist/anime/{malId}.json");
            var jObject = JsonNode.Parse(json);
            var key = GetKey(provider);
            var pages = jObject["Pages"];

            if(pages is null)
            {
                return null;
            }

            var providerPages = pages[key];

            if(providerPages is null)
            {
                return null;
            }

            var providerPageObj = providerPages.AsObject();

            if(providerPageObj.Count == 0)
            {
                return null;
            }
            else if(providerPageObj.Count == 1)
            {
                return (GetSearchResult(providerPageObj.ElementAt(0).Value), null);
            }
            else if(providerPageObj.Count == 2)
            {
                var first = GetSearchResult(providerPageObj.ElementAt(0).Value);
                var second = GetSearchResult(providerPageObj.ElementAt(1).Value);
                
                return second.Title.ToLower().Contains("dub")
                    ? (first, second)
                    : (second, first);
            }
            else
            {
                return null;
            }

        }

        private static SearchResult GetSearchResult(JsonNode node)
        {
            return new SearchResult
            {
                Title = (string)node["title"].AsValue(),
                Url = (string)node["url"].AsValue()
            };
        }

        private static string GetKey(ProviderType provider)
        {
            return provider switch
            {
                ProviderType.GogoAnime => "Gogoanime",
                ProviderType.Yugen => "YugenAnime",
                ProviderType.Zoro => "Zoro",
                ProviderType.AnimixPlay => "AniMixPlay",
                ProviderType.Tenshi => "Tenshi",
                ProviderType.AnimePahe => "animepahe",
                _ => throw new UnreachableException()
            };
        }
    }
}
