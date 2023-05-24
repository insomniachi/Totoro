using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using Flurl;
using Flurl.Http;
using FlurlGraphQL.Querying;
using Splat;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;

namespace Totoro.Plugins.Anime.AllAnime;

public partial class StreamProvider : IMultiLanguageAnimeStreamProvider, IEnableLogger
{
    public const string SHOW_QUERY =
        """
        query ($showId: String!) {
            show(
                _id: $showId
            ) {
                availableEpisodesDetail
            }
        }
        """;

    public const string EPISODE_QUERY =
        """
        query ($showId: String!, $translationType: VaildTranslationTypeEnumType!, $episodeString: String!) {
            episode(
                showId: $showId
                translationType: $translationType
                episodeString: $episodeString
            ) {
                episodeString,
                sourceUrls,
                notes
            }
        }
        """;

    [GeneratedRegex(@"^#EXT-X-STREAM-INF:.*?RESOLUTION=\d+x(?'resolution'\d+).*?\n(?'url'.+?)$", RegexOptions.Multiline)]
    private static partial Regex VrvResponseRegex();

    [GeneratedRegex(@"https://.+?/(?'base'.+?/),(?'resolutions'(?:\d+p,)+)")]
    private static partial Regex WixMpUrlRegex();

    [GeneratedRegex("\\\\u(?<Value>[a-zA-Z0-9]{4})")]
    private static partial Regex UtfEncodedStringRegex();

    public async Task<int> GetNumberOfStreams(string url, StreamType streamType)
    {
        var jObject = await Config.Api
            .WithGraphQLQuery(SHOW_QUERY)
            .SetGraphQLVariable("showId", url.Split('/').LastOrDefault()?.Trim())
            .PostGraphQLQueryAsync()
            .ReceiveGraphQLRawJsonResponse();

        var episodeDetails = jObject?["show"]?["availableEpisodesDetail"];

        if (episodeDetails is null)
        {
            this.Log().Error("availableEpisodesDetail not found");
            return 0;
        }

        var sorted = GetEpisodes(episodeDetails.ToObject<EpisodeDetails>() ?? new(), streamType).OrderBy(x => x.Length).ThenBy(x => x).ToList();
        var total = int.Parse(sorted.LastOrDefault(x => int.TryParse(x, out int e))!);

        return total;
    }

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range range, StreamType streamType)
    {
        var versionResponse = await Config.Url.AppendPathSegment("getVersion").GetJsonAsync<GetVersionResponse>();
        var apiEndPoint = new Url(versionResponse?.episodeIframeHead ?? "");
        var id = url.Split('/').LastOrDefault()?.Trim();

        var jObject = await Config.Api
            .WithGraphQLQuery(SHOW_QUERY)
            .SetGraphQLVariable("showId", id)
            .PostGraphQLQueryAsync()
            .ReceiveGraphQLRawJsonResponse();

        var episodeDetails = jObject?["show"]?["availableEpisodesDetail"];

        if (episodeDetails is null)
        {
            this.Log().Error("availableEpisodesDetail not found");
            yield break;
        }

        if(episodeDetails.ToObject<EpisodeDetails>() is not { } episodesDetail)
        {
            yield break;
        }

        var sorted = GetEpisodes(episodesDetail!, Config.StreamType).OrderBy(x => x.Length).ThenBy(x => x).ToList();
        var total = int.Parse(sorted.LastOrDefault(x => int.TryParse(x, out int e))!);
        var (start, end) = range.Extract(total);
        foreach (var ep in sorted)
        {
            if (int.TryParse(ep, out int e))
            {
                if (e < start)
                {
                    continue;
                }
                else if (e > end)
                {
                    break;
                }
            }

            var streamTypes = new List<StreamType>() { StreamType.EnglishSubbed };

            if(episodesDetail.dub?.Contains(ep) == true)
            {
                streamTypes.Add(StreamType.EnglishDubbed);
            }
            if(episodesDetail.raw?.Contains(ep) == true)
            {
                streamTypes.Add(StreamType.Raw);
            }

            var jsonNode = await Config.Api
                .WithGraphQLQuery(EPISODE_QUERY)
                .SetGraphQLVariables(new
                {
                    showId = id,
                    translationType = streamType.ConvertToTranslationType(),
                    episodeString = ep
                })
                .PostGraphQLQueryAsync()
                .ReceiveGraphQLRawJsonResponse();

            if (jsonNode?["errors"] is { })
            {
                this.Log().Warn("Error : " + jsonNode.ToString());
            }

            var sourceArray = jsonNode?["episode"]?["sourceUrls"];
            var sourceObjs = sourceArray?.ToObject<List<SourceUrlObj>>() ?? new List<SourceUrlObj>();

            var source = sourceObjs.Where(x => x.type == "iframe").OrderByDescending(x => x.priority).First();
            var parsedUrl = DecodeEncodedNonAsciiCharacters(HttpUtility.UrlDecode(source.sourceUrl.Replace("clock", "clock.json")));
            var uri = new Uri(parsedUrl, UriKind.RelativeOrAbsolute);

            var streamUrl = parsedUrl;
            if (!uri.IsAbsoluteUri)
            {
                streamUrl = apiEndPoint.Host + parsedUrl;
            }

            var stream = await Extract(streamUrl);
            if (stream is { })
            {
                if (jsonNode?["episode"]?["notes"]?.ToString() is { } note)
                {
                    stream.AdditionalInformation.Title = note;
                }

                stream.Episode = e;
                stream.EpisodeString = ep;
                stream.StreamTypes.AddRange(streamTypes);
                yield return stream;
            }
        }
    }

    private async Task<VideoStreamsForEpisode?> Extract(string url)
    {
        if(string.IsNullOrEmpty(url))
        {
            return null;
        }

        if(!url.StartsWith("https"))
        {
            url = $"https://{url}";
        }

        var json = await url.GetStringAsync();
        var jObject = JsonNode.Parse(json);
        var links = jObject!["links"]!.Deserialize<List<StreamLink>>()!;

        if(string.IsNullOrEmpty(links[0].link))
        {
            var result = new VideoStreamsForEpisode();
            var resolutionStr = GetResolution(links[0]);
            if (links[0].portData.streams.FirstOrDefault(x => x is { hardsub_lang: "en-US", format: "adaptive_hls" or "hls" }) is { } obj)
            {
                result.Streams.Add(new VideoStream
                {
                    Url = obj.url,
                    Resolution = resolutionStr
                });
            }
            return result;
        }

        var uri = new Uri(links[0].link);

        return uri.Host switch
        {
            "v.vrv.co" => await VrvUnpack(uri),
            "repackager.wixmp.com" => WixMpUnpack(uri),
            _ => GetDefault(links)
        };
    }

    private static string GetResolution(StreamLink link)
    {
        if(link.resolution > 0)
        {
            return link.resolution.ToString();
        }
        if(!string.IsNullOrEmpty(link.resolutionStr))
        {
            return link.resolutionStr;
        }
        return "default";
    }

    private static VideoStreamsForEpisode GetDefault(IEnumerable<StreamLink> links)
    {
        var result = new VideoStreamsForEpisode();
        foreach (var item in links)
        {
            var resolutionStr = GetResolution(item);
            var uri = new Uri(item.link);
            result.Streams.Add(new VideoStream
            {
                Url = uri.AbsoluteUri,
                Resolution = resolutionStr
            });
        }
        return result;
    }
    
    private async Task<VideoStreamsForEpisode> VrvUnpack(Uri uri)
    {
        var response = await uri.GetStringAsync();

        var result = new VideoStreamsForEpisode();
        foreach (var match in VrvResponseRegex().Matches(response).Cast<Match>())
        {
            var streamUrl = match.Groups["url"].Value.Replace("/index-v1-a1.m3u8", "");
            var quality = match.Groups["resolution"].Value;
            result.Streams.Add(new VideoStream { Resolution = quality, Url = streamUrl });
        }

        return result;
    }

    private static VideoStreamsForEpisode? WixMpUnpack(Uri uri)
    {
        var match = WixMpUrlRegex().Match(uri.AbsoluteUri);
        
        if(!match.Success)
        {
            return null;
        }

        var baseUrl = match.Groups["base"].Value;

        var result = new VideoStreamsForEpisode();

        foreach (var resolution in match.Groups["resolutions"].Value.Split(",", StringSplitOptions.RemoveEmptyEntries))
        {
            result.Streams.Add(new VideoStream { Url = $"https://{baseUrl}{resolution}/mp4/file.mp4", Resolution = resolution });
        }

        return result;
    }

    private static List<string> GetEpisodes(EpisodeDetails episodeDetails, StreamType streamType)
    {
        return streamType switch
        {
            StreamType.EnglishSubbed => episodeDetails.sub,
            StreamType.EnglishDubbed => episodeDetails.dub,
            StreamType.Raw => episodeDetails.raw,
            _ => throw new NotSupportedException(streamType.ToString())
        };
    }

    static string DecodeEncodedNonAsciiCharacters(string value)
    {
        return UtfEncodedStringRegex().Replace(value, m => ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString());
    }
}
