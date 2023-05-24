using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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

        public async Task<long> GetId(string identifier, string provider)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                this.Log().Warn("No identifier found");
                return 0;
            }

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
                var value = (long)jObject[key].AsValue();
                return value;
            }
            catch (Exception ex)
            {
                this.Log().Error(ex);
                return 0;
            }
        }

        public async Task<long?> GetIdFromUrl(string url, string provider)
        {
            if (provider == "yugen")
            {
                var regex = _settings.DefaultListService switch
                {
                    ListServiceType.MyAnimeList => YugenMalIdRegex(),
                    ListServiceType.AniList => YugenAnilistIdRegex(),
                    _ => null
                };

                return await GetIdFromSource(url, regex);
            }
            else if (provider == "gogo")
            {
                var uri = new Uri(url);
                var path = uri.Segments[^1];

                if (path.Contains("-episode-"))
                {
                    return await GetId(path.Split("-episode").FirstOrDefault(), provider);
                }
                else
                {
                    return await GetId(path, provider);
                }
            }
            else if (provider == "animepahe")
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
                "gogo" => "Gogoanime",
                "yugen" => "YugenAnime",
                "zoro" => "Zoro",
                "animepahe" => "animepahe",
                _ => string.Empty
            };
        }

        private static string GetProviderPage(string provider)
        {
            return provider switch
            {
                "gogo" => "Gogoanime",
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
    }
}
