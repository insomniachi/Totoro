using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicData.Aggregation;
using FuzzySharp;
using HtmlAgilityPack;
using Splat;
using Totoro.Core.Contracts;
using Totoro.Core.Models;

namespace Totoro.Core.Services
{
    public partial class StreamPageMapper : IStreamPageMapper, IEnableLogger
    {
        private readonly HttpClient _httpClient;
        private readonly IAnimeIdService _animeIdService;
        private readonly ISettings _settings;

        public StreamPageMapper(HttpClient httpClient,
                                IAnimeIdService animeIdService,
                                ISettings settings)
        {
            _httpClient = httpClient;
            _animeIdService = animeIdService;
            _settings = settings;
        }

        public async Task<long> GetId(string identifier, ProviderType provider)
        {
            try
            {
                var key = _settings.DefaultListService switch
                {
                    ListServiceType.MyAnimeList => "malId",
                    ListServiceType.AniList => "aniId",
                    _ => throw new NotSupportedException()
                };

                var json = await _httpClient.GetStringAsync($"https://raw.githubusercontent.com/MALSync/MAL-Sync-Backup/master/data/pages/{GetProviderPage(provider)}/{identifier}.json");
                var jObject = JsonNode.Parse(json);
                var value = (long)jObject["malId"].AsValue();
                return value;
            }
            catch (Exception ex)
            {
                this.Log().Error(ex);
                return 0;
            }
        }

        public async Task<long?> GetIdFromUrl(string url, ProviderType provider)
        {
            if (provider == ProviderType.Yugen)
            {
                var regex = _settings.DefaultListService switch
                {
                    ListServiceType.MyAnimeList => YugenMalIdRegex(),
                    ListServiceType.AniList => YugenAnilistIdRegex(),
                    _ => null
                };

                return await GetIdFromSource(url, regex);
            }
            else if (provider == ProviderType.GogoAnime)
            {
                var uri = new Uri(url);
                var match = GogoAnimeIdentifierRegex().Match(uri.AbsolutePath);
                return await GetId(match.Groups[1].Value, ProviderType.GogoAnime);
            }
            else if (provider == ProviderType.AnimePahe)
            {
                try
                {
                    var html = await _httpClient.GetStringAsync(url);
                    var result = new AnimeId();

                    foreach (var match in AnimePaheAnimeIdRegex().Matches(html).Cast<Match>().Where(x => x.Success))
                    {
                        var id = long.Parse(match.Groups["Id"].Value);
                        switch (match.Groups["Type"].Value)
                        {
                            case "anidb":
                                result.AniDb = id;
                                break;
                            case "anilist":
                                result.AniList = id;
                                break;
                            case "kitsu":
                                result.Kitsu = id;
                                break;
                            case "myanimelist":
                                result.MyAnimeList = id;
                                break;
                        }
                    }

                    return GetId(result);
                }
                catch (Exception ex)
                {
                    this.Log().Error(ex);
                    return null;
                }
            }
            else if (provider == ProviderType.AllAnime)
            {
                var aniListId = await GetIdFromSource(url, AllAnimeAniListIdRegex());
                if (aniListId == 0)
                {
                    return null;
                }
                var animeId = await _animeIdService.GetId(ListServiceType.AniList, aniListId);
                return GetId(animeId);
            }
            else
            {
                return null;
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

        private long GetId(AnimeId animeId)
        {
            return _settings.DefaultListService switch
            {
                ListServiceType.AniList => animeId.AniList,
                ListServiceType.MyAnimeList => animeId.MyAnimeList,
                ListServiceType.Kitsu => animeId.Kitsu,
                ListServiceType.AniDb => animeId.AniDb,
                _ => throw new NotSupportedException()
            };
        }

        public async Task<(SearchResult Sub, SearchResult Dub)?> GetStreamPage(long id, ProviderType provider)
        {
            var listService = _settings.DefaultListService switch
            {
                ListServiceType.MyAnimeList => "myanimelist",
                ListServiceType.AniList => "anilist",
                _ => throw new NotSupportedException()
            };

            var json = await _httpClient.GetStringAsync($"https://raw.githubusercontent.com/MALSync/MAL-Sync-Backup/master/data/{listService}/anime/{id}.json");
            var jObject = JsonNode.Parse(json);
            var key = GetKey(provider);
            var pages = jObject["Pages"];

            if (pages is null)
            {
                return null;
            }

            var providerPages = pages[key];

            if (providerPages is null)
            {
                return null;
            }

            var providerPageObj = providerPages.AsObject();

            if (providerPageObj.Count == 0)
            {
                return null;
            }
            else if (providerPageObj.Count == 1)
            {
                return (GetSearchResult(providerPageObj.ElementAt(0).Value), null);
            }
            else if (providerPageObj.Count == 2)
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

        [GeneratedRegex(@"anilist.co/anime/(\d+)")]
        private static partial Regex YugenAnilistIdRegex();

        [GeneratedRegex("/?(.+)-episode-\\d+")]
        private static partial Regex GogoAnimeIdentifierRegex();

        [GeneratedRegex(@"<meta name=""(?'Type'anidb|anilist|kitsu|myanimelist)"" content=""(?'Id'\d+)"">")]
        private static partial Regex AnimePaheAnimeIdRegex();

        [GeneratedRegex(@"banner/(\d+)")]
        private static partial Regex AllAnimeAniListIdRegex();
    }
}
