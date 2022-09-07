using System.Globalization;
using System.Text.Json.Nodes;

namespace AnimDL.UI.Core.Services;

public class AnimixPlayEpisodesProvider : IRecentEpisodesProvider
{
    private readonly HttpClient _httpClient;
    private readonly IAnimeService _animeService;

    public AnimixPlayEpisodesProvider(HttpClient httpClient,
                                      IAnimeService animeService)
    {
        _httpClient = httpClient;
        _animeService = animeService;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.UserAgent);
    }

    public IObservable<IEnumerable<AiredEpisode>> GetRecentlyAiredEpisodes()
    {
        return Observable.Create<IEnumerable<AiredEpisode>>(async observer =>
        {
            var anime = await _animeService.GetAiringAnime();
            var hasMore = false;
            var now = DateTime.UtcNow;
            var time = now;

            do
            {
                Dictionary<string, string> postData = new()
                {
                    ["seasonal"] = time.ToString("yyyy-MM-dd HH:mm:ss")
                };

                using var content = new FormUrlEncodedContent(postData);
                content.Headers.Clear();
                content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

                using var request = new HttpRequestMessage(HttpMethod.Post, @"https://animixplay.to/api/search");
                request.Content = content;
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    observer.OnError(new Exception("http resonse doesn't contain success code"));
                }

                var result = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(result))
                {
                    observer.OnError(new Exception("empty json response"));
                }

                var jObject = JsonNode.Parse(result);
                var episodes = jObject["result"].AsArray();

                if (episodes is null)
                {
                    observer.OnError(new Exception("no episodes"));
                }

                var models = new List<AiredEpisode>();

                foreach (var item in episodes)
                {
                    try
                    {
                        var title = (string)item["title"].AsValue();

                        if (anime.FirstOrDefault(x => FuzzySharp.Fuzz.PartialRatio(title, x.Title) > 80 ||
                                                     x.AlternativeTitles.Any(x => FuzzySharp.Fuzz.PartialRatio(title, x) > 80)) is { } animeModel)
                        {
                            var model = new AiredEpisode
                            {
                                Anime = title,
                                EpisodeUrl = $"https://animixplay.to{(string)item["url"].AsValue()}",
                                InfoText = (string)item["infotext"].AsValue(),
                                TimeOfAiring = DateTime.ParseExact((string)item["timetop"].AsValue(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).ToLocalTime(),
                                Image = (string)item["picture"].AsValue(),
                                Model = animeModel
                            };

                            models.Add(model);
                        }
                    }
                    catch { }
                }

                hasMore = (bool)jObject["more"].AsValue();
                time = DateTime.ParseExact((string)jObject["last"].AsValue(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                observer.OnNext(models);

                var days = (now - time).TotalDays;

            } while (hasMore && (now - time).TotalDays <= 7);

            observer.OnCompleted();

            return Disposable.Empty;
        });
    }
}
