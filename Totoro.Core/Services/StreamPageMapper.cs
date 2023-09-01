using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl.Http;
using Splat;
using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Services
{
    public partial class StreamPageMapper : IStreamPageMapper, IEnableLogger
    {
        private readonly HttpClient _httpClient;
        private readonly IAnimeIdService _animeIdService;
        private readonly ISettings _settings;

        class SearchResult : ICatalogItem
        {
            required public string Title { get; init; }
            required public string Url { get; init; }
        }

        public StreamPageMapper(HttpClient httpClient,
                                IAnimeIdService animeIdService,
                                ISettings settings)
        {
            _httpClient = httpClient;
            _animeIdService = animeIdService;
            _settings = settings;
        }

        public Task<long> GetId(string identifier, string provider)
        {
            return Task.FromResult<long>(0);

            // Mal-Sync-Backup got DMCA
            //if (string.IsNullOrEmpty(identifier))
            //{
            //    this.Log().Warn("No identifier found");
            //    return 0;
            //}

            //try
            //{
            //    var key = _settings.DefaultListService switch
            //    {
            //        ListServiceType.MyAnimeList => "malId",
            //        ListServiceType.AniList => "aniId",
            //        _ => throw new NotSupportedException()
            //    };

            //    var json = await _httpClient.GetStringAsync($"https://raw.githubusercontent.com/MALSync/MAL-Sync-Backup/master/data/pages/{GetProviderPage(provider)}/{identifier}.json");
            //    var jObject = JsonNode.Parse(json);
            //    var value = (long)jObject[key].AsValue();
            //    return value;
            //}
            //catch (Exception ex)
            //{
            //    this.Log().Error(ex);
            //    return 0;
            //}
        }

        public async Task<long?> GetIdFromUrl(string url, string provider)
        {
            try
            {
                if (provider == "yugen-anime")
                {
                    var regex = _settings.DefaultListService switch
                    {
                        ListServiceType.MyAnimeList => YugenMalIdRegex(),
                        ListServiceType.AniList => YugenAnilistIdRegex(),
                        _ => null
                    };

                    return await GetIdFromSource(url, regex);
                }
                else if (provider == "gogo-anime")
                {
                    return null;
                }
                else if (provider == "anime-pahe")
                {
                    var html = await _httpClient.GetStringAsync(url);
                    var animeId = new AnimeId();

                    foreach (var match in AnimePaheAnimeIdRegex().Matches(html).Cast<Match>().Where(x => x.Success))
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

                    return GetId(animeId);
                }
                else if (provider == "allanime")
                {
                    var aniListId = await GetIdFromSource(url, AllAnimeAniListIdRegex());
                    if (aniListId == 0)
                    {
                        return null;
                    }
                    var animeId = await _animeIdService.GetId(ListServiceType.AniList, aniListId);
                    return GetId(animeId);
                }
                else if (provider == "zoro")
                {
                    var html = await url.GetStringAsync();
                    var animeId = new AnimeId();

                    foreach (var match in ZoroAnimeIdRegex().Matches(html).Cast<Match>().Where(x => x.Success))
                    {
                        var id = long.Parse(match.Groups["Id"].Value);
                        switch (match.Groups["Type"].Value)
                        {
                            case "anilist_id":
                                animeId.AniList = id;
                                break;
                            case "mal_id":
                                animeId.MyAnimeList = id;
                                break;
                        }
                    }
                    return GetId(animeId);
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                this.Log().Warn(ex);
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
                ListServiceType.Kitsu => animeId.Kitsu ?? throw new ArgumentException("Kitsu id not found"),
                ListServiceType.AniDb => animeId.AniDb ?? throw new ArgumentException("AniDb id not found"),
                _ => throw new NotSupportedException()
            };
        }

        public async Task<(ICatalogItem Sub, ICatalogItem Dub)?> GetStreamPage(long id, string provider)
        {
            var listService = _settings.DefaultListService switch
            {
                ListServiceType.MyAnimeList => "myanimelist",
                ListServiceType.AniList => "anilist",
                _ => throw new NotSupportedException()
            };

            var url = $"https://raw.githubusercontent.com/MALSync/MAL-Sync-Backup/master/data/{listService}/anime/{id}.json";
            var result = await _httpClient.GetAsync(url);

            if (!result.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await result.Content.ReadAsStringAsync();
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

        private static ICatalogItem GetSearchResult(JsonNode node)
        {
            return new SearchResult
            {
                Title = (string)node["title"].AsValue(),
                Url = (string)node["url"].AsValue()
            };
        }

        private static string GetKey(string provider)
        {
            return provider switch
            {
                "gogo-anime" => "Gogoanime",
                "yugen-anime" => "YugenAnime",
                "zoro" => "Zoro",
                "anime-pahe" => "animepahe",
                "marin" => "Marin",
                _ => string.Empty
            };
        }

        private static string GetProviderPage(string provider)
        {
            return provider switch
            {
                "gogo-anime" => "Gogoanime",
                _ => throw new NotSupportedException()
            };
        }

        [GeneratedRegex("\"mal_id\":(\\d+)")]
        private static partial Regex YugenMalIdRegex();

        [GeneratedRegex(@"anilist.co/anime/(\d+)")]
        private static partial Regex YugenAnilistIdRegex();


        [GeneratedRegex(@"<meta name=""(?'Type'anidb|anilist|kitsu|myanimelist)"" content=""(?'Id'\d+)"">")]
        private static partial Regex AnimePaheAnimeIdRegex();

        [GeneratedRegex(@"banner/(\d+)")]
        private static partial Regex AllAnimeAniListIdRegex();

        [GeneratedRegex(@"""(?<Type>mal_id|anilist_id)"":""(?<Id>\d+)""")]
        private static partial Regex ZoroAnimeIdRegex();
    }
}
