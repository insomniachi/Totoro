using System.Text.Json.Nodes;
using System.Web;
using Flurl;
using Flurl.Http;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.Marin;

internal class StreamProvider : IAnimeStreamProvider
{
    public async Task<int> GetNumberOfStreams(string url)
    {
        var version = await Config.GetInertiaVersion();
        var jar = Config.GetCookieJar();
        var animeData = await url
            .WithCookies(jar)
            .WithHeader("x-inertia", true)
            .WithHeader("x-inertia-version", version)
            .GetStringAsync();

        var jObject = JsonNode.Parse(animeData);
        return (int)double.Parse(jObject?["props"]?["anime"]?["last_episode"]?.ToString() ?? "0");
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range episodeRange)
    {
        var count = await GetNumberOfStreams(url);
        var (start, end) = episodeRange.Extract(count);
        for (int i = start; i <= end; i++)
        {
            var stream = await GetEpStream(Url.Combine(url, i.ToString()));
            stream.Episode = i;
            yield return stream;
        }
    }

    private static async Task<VideoStreamsForEpisode> GetEpStream(string url)
    {
        var version = await Config.GetInertiaVersion();
        var jar = Config.GetCookieJar();
        var episdoeData = await url
            .WithCookies(jar)
            .WithHeader("x-inertia", true)
            .WithHeader("x-inertia-version", version)
            .GetStringAsync();

        var jObject = JsonNode.Parse(episdoeData);
        var stream = new VideoStreamsForEpisode();
        var token = HttpUtility.UrlDecode(jar.First(x => x.Name == "XSRF-TOKEN").Value);
        stream.AdditionalInformation.Title = jObject?["props"]?["episode"]?["data"]?["title"]?.AsArray()[0]?["text"]?.ToString();
        foreach (var item in jObject?["props"]?["video"]?["data"]?["mirror"]?.AsArray() ?? [])
        {
            var code = item?["code"];
            stream.Streams.Add(new VideoStream
            {
                Url = code!["file"]!.ToString(),
                Resolution = item!["resolution"]!.ToString(),
                Headers =
                {
                    { HeaderNames.Accept, "video/webm,video/ogg,video/*;q=0.9,application/ogg;q=0.7,audio/*;q=0.6,*/*;q=0.5" },
                    { HeaderNames.Referer, url },
                    { HeaderNames.AcceptLanguage, "en-US,en;q=0.5" },
                    { HeaderNames.Cookie, string.Join(";", jar.Select(x => $"{x.Name}={x.Value}")) },
                    { "x-xsrf-token", token },
                }
            });
        }

        return stream;
    }
}
