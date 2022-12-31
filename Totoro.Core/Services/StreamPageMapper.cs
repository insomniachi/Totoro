using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Splat;

namespace Totoro.Core.Services
{
    public partial class StreamPageMapper : IStreamPageMapper, IEnableLogger
    {
        private readonly HttpClient _httpClient;
        private readonly IAnimeIdService _animeIdService;

        public StreamPageMapper(HttpClient httpClient, 
                                IAnimeIdService animeIdService)
        {
            _httpClient = httpClient;
            _animeIdService = animeIdService;
        }

        public async Task<long> GetMalId(string identifier, ProviderType provider)
        {
            try
            {
                var json = await _httpClient.GetStringAsync($"https://raw.githubusercontent.com/MALSync/MAL-Sync-Backup/master/data/pages/{GetProviderPage(provider)}/{identifier}.json");
                var jObject = JsonNode.Parse(json);
                var value = (long)jObject["malId"].AsValue();
                return value;
            }
            catch(Exception ex)
            {
                this.Log().Error(ex);
                return 0;
            }
        }

        public async Task<long> GetMalIdFromUrl(string url, ProviderType provider)
        {
            if(provider == ProviderType.Yugen)
            {
                return await GetIdFromSource(url, YugenMalIdRegex());
            }
            else if(provider == ProviderType.GogoAnime)
            {
                var uri = new Uri(url);
                var match = GogoAnimeIdentifierRegex().Match(uri.AbsolutePath);
                return await GetMalId(match.Groups[1].Value, ProviderType.GogoAnime);
            }
            else if(provider == ProviderType.AnimePahe)
            {
                return await GetIdFromSource(url, AnimePaheMalIdRegex());
            }
            else if(provider == ProviderType.AllAnime)
            {
                var aniListId = await GetIdFromSource(url, AllAnimeAniListIdRegex());
                if(aniListId == 0)
                {
                    return 0;
                }
                var animeId = await _animeIdService.GetId(ListServiceType.AniList, aniListId);
                return animeId.MyAnimeList;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private async Task<long> GetIdFromSource(string url, Regex regex)
        {
            try
            {
                var html = await _httpClient.GetStringAsync(url);
                var match = regex.Match(html);
                return match.Success ? long.Parse(match.Groups[1].Value) : 0;
            }
            catch (Exception ex)
            {
                this.Log().Error(ex);
                return 0;
            }
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
                _ => string.Empty
            };
        }

        private static string GetProviderPage(ProviderType provider)
        {
            return provider switch
            {
                ProviderType.GogoAnime => "Gogoanime",
                ProviderType.Tenshi => "Tenshi",
                _ => throw new NotSupportedException()
            };
        }

        [GeneratedRegex("\"mal_id\":(\\d+)")]
        private static partial Regex YugenMalIdRegex();

        [GeneratedRegex("/?(.+)-episode-\\d+")]
        private static partial Regex GogoAnimeIdentifierRegex();

        [GeneratedRegex(@"myanimelist\.net/anime/(\d+)")]
        private static partial Regex AnimePaheMalIdRegex();

        [GeneratedRegex(@"banner/(\d+)")]
        private static partial Regex AllAnimeAniListIdRegex();
    }
}
