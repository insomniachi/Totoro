using System.Globalization;
using System.Text.Json.Nodes;

namespace AnimDL.UI.Core.Services;

public class AnimixPlayEpisodesProvider : IRecentEpisodesProvider
{
    private readonly HttpClient _httpClient;

    public AnimixPlayEpisodesProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.UserAgent);
    }

    public IObservable<IEnumerable<AiredEpisode>> GetRecentlyAiredEpisodes()
    {
        return Observable.Create<IEnumerable<AiredEpisode>>(async observer =>
        {
            Dictionary<string, string> postData = new()
            {
                ["seasonal"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
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
                    var model = new AiredEpisode
                    {
                        Anime = (string)item["title"].AsValue(),
                        EpisodeUrl = $"https://animixplay.to{(string)item["url"].AsValue()}",
                        InfoText = (string)item["infotext"].AsValue(),
                        TimeOfAiring = DateTime.ParseExact((string)item["timetop"].AsValue(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).ToLocalTime(),
                        Image = (string)item["picture"].AsValue()
                    };

                    models.Add(model);
                }
                catch { }
            }

            observer.OnNext(models);

            observer.OnCompleted();

            return Disposable.Empty;
        });
    }
}
