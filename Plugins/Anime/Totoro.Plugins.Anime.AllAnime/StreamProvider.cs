﻿using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using FlurlGraphQL;
using Splat;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AllAnime;

internal partial class StreamProvider : IMultiLanguageAnimeStreamProvider, IAnimeStreamProvider, IEnableLogger
{
    public const string SHOW_QUERY =
        """
        query ($showId: String!) {
            show(
                _id: $showId
            ) {
                availableEpisodesDetail,
                malId,
                aniListId
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

    [GeneratedRegex("(?<=/clock)(?=[?&#])")]
    private static partial Regex ClockRegex();

    public Task<int> GetNumberOfStreams(string url) => GetNumberOfStreams(url, ConfigManager<Config>.Current.StreamType);

    public async Task<int> GetNumberOfStreams(string url, StreamType streamType)
    {
        var jObject = await ConfigManager<Config>.Current.Api
            .WithGraphQLQuery(SHOW_QUERY)
            .SetGraphQLVariable("showId", url.Split('/').LastOrDefault()?.Trim())
            .PostGraphQLQueryAsync()
            .ReceiveGraphQLRawSystemTextJsonResponse();

        var episodeDetails = jObject?["show"]?["availableEpisodesDetail"] as JsonObject;

        if (episodeDetails is null)
        {
            this.Log().Error("availableEpisodesDetail not found");
            return 0;
        }

        var sorted = GetEpisodes(episodeDetails.Deserialize<EpisodeDetails>() ?? new(), streamType)
                .OrderBy(x => x.Length)
                .ThenBy(x => x)
                .ToList();

        var total = int.Parse(sorted.LastOrDefault(x => int.TryParse(x, out int e))!);

        return total;
    }

    public IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range range) => GetStreams(url, range, ConfigManager<Config>.Current.StreamType);

    public async IAsyncEnumerable<VideoStreamsForEpisode> GetStreams(string url, Range range, StreamType streamType)
    {
        var id = url.Split('/').LastOrDefault()?.Trim();

        var jObject = await ConfigManager<Config>.Current.Api
            .WithGraphQLQuery(SHOW_QUERY)
            .SetGraphQLVariable("showId", id)
            .PostGraphQLQueryAsync()
            .ReceiveGraphQLRawSystemTextJsonResponse();

        var episodeDetails = jObject?["show"]?["availableEpisodesDetail"];

        if (episodeDetails is null)
        {
            this.Log().Error("availableEpisodesDetail not found");
            yield break;
        }

        if (episodeDetails.Deserialize<EpisodeDetails>() is not { } episodesDetail)
        {
            yield break;
        }

        var sorted = GetEpisodes(episodesDetail!, ConfigManager<Config>.Current.StreamType).OrderBy(x => x.Length).ThenBy(x => x).ToList();
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
            else
            {
                continue;
            }

            var streamTypes = new List<StreamType>() { StreamType.Subbed(Languages.English) };

            if (episodesDetail.dub?.Contains(ep) == true)
            {
                streamTypes.Add(StreamType.Dubbed(Languages.English));
            }
            if (episodesDetail.raw?.Contains(ep) == true)
            {
                streamTypes.Add(StreamType.Raw());
            }

            var jsonNode = await ConfigManager<Config>.Current.Api
                .WithGraphQLQuery(EPISODE_QUERY)
                .SetGraphQLVariables(new
                {
                    showId = id,
                    translationType = streamType.ConvertToTranslationType(),
                    episodeString = ep
                })
                .PostGraphQLQueryAsync()
                .ReceiveGraphQLRawSystemTextJsonResponse();

            if (jsonNode?["errors"] is { })
            {
                this.Log().Warn("Error : " + jsonNode.ToString());
            }

            var sourceArray = jsonNode?["episode"]?["sourceUrls"];
            var sourceObjs = sourceArray?.Deserialize<List<SourceUrlObj>>() ?? [];
            sourceObjs.Sort((x, y) => y.priority.CompareTo(x.priority));

            foreach (var item in sourceObjs)
            {
                item.sourceUrl = DecryptSourceUrl(item.sourceUrl);

				switch (item.sourceName)
				{
					case "Mp4":
						yield return await FromMp4Upload(item.sourceUrl, e);
                        yield break;
					case "Yt-mp4":
						yield return WithReferer(item.sourceUrl, "https://allanime.day/", e);
						yield break;
					case "Vg":
						continue;
					case "Fm-Hls":
						continue;
					case "Sw":
						continue;
					case "Ok":
						continue;
					default:
						break;
				}

				if (item.type == "iframe" && item.sourceUrl.Contains("clock", StringComparison.OrdinalIgnoreCase))
                {
                    var clockUrl = ClockRegex().Replace("https://allanime.day" + item.sourceUrl, ".json");
					var stream = await Extract(clockUrl);
                    if (stream is not null)
                    {
                        stream.Episode = e;
                        stream.StreamTypes.AddRange(streamTypes);
                        yield return stream;
                        yield break;
                    }
                }
            }
        }
    }

    private async Task<VideoStreamsForEpisode?> Extract(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (!url.StartsWith("https"))
        {
            url = $"https://{url}";
        }

        var json = await url.GetStringAsync();
        var jObject = JsonNode.Parse(json);
        var links = jObject!["links"]!.Deserialize<List<StreamLink>>()!;

        if (string.IsNullOrEmpty(links[0].link))
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

    private async Task<VideoStreamsForEpisode?> Unpack(string url)
    {
        var uri = new Uri(url);

        return uri.Host switch
        {
            "v.vrv.co" => await VrvUnpack(uri),
            "repackager.wixmp.com" => WixMpUnpack(uri),
            _ => GetDefault(url)
        };
    }

    private static string GetResolution(StreamLink link)
    {
        if (link.resolution > 0)
        {
            return link.resolution.ToString();
        }
        if (!string.IsNullOrEmpty(link.resolutionStr))
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
    
    private static VideoStreamsForEpisode GetDefault(string url)
    {
        var result = new VideoStreamsForEpisode();
        var uri = new Uri(url);
        result.Streams.Add(new VideoStream
        {
            Url = uri.AbsoluteUri,
            Resolution = "default"
        });
        return result;
    }

    private static async Task<VideoStreamsForEpisode> VrvUnpack(Uri uri)
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

        if (!match.Success)
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
            { AudioLanguage: Languages.Japanese, SubtitleLanguage: Languages.English } => episodeDetails.sub,
            { AudioLanguage: Languages.English, SubtitleLanguage: _ } => episodeDetails.dub,
            { AudioLanguage: Languages.Japanese, SubtitleLanguage: "" } => episodeDetails.raw,
            _ => throw new UnreachableException(streamType.ToString())
        };
    }

    static string DecodeEncodedNonAsciiCharacters(string value)
    {
        return UtfEncodedStringRegex().Replace(value, m => ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString());
    }

    private static string Decrypt(string target) => string.Join("", Convert.FromHexString(target).Select(x => (char)(x ^ 56)));

    private static string DecryptSourceUrl(string sourceUrl)
    {
        var index = sourceUrl.LastIndexOf('-') + 1;
        var encrypted = sourceUrl[index..];
        return Decrypt(encrypted);
    }

	[GeneratedRegex("video/mp4\\\",src:\\\"(https?://.*/video\\.mp4)\\\"")]
	private static partial Regex Mp4JuicyServerRegex();

	internal static async Task<VideoStreamsForEpisode> FromMp4Upload(string url, int episode)
	{
		var response = await url.GetStringAsync();
		var match = Mp4JuicyServerRegex().Match(response.Replace(" ", "").Replace("\n", ""));
        return new VideoStreamsForEpisode()
        {
            Episode = episode,
			Streams =
            {
                new VideoStream()
                {
                    Url = match.Groups[1].Value,
                    Resolution = "default",
                    Headers=
                    {
                        [HeaderNames.Referer.ToLower()] = "https://www.mp4upload.com/",
                    }
                }
            }
        };
	}

	internal static VideoStreamsForEpisode WithReferer(string url, string referer, int episode)
	{
		return new VideoStreamsForEpisode
		{
            Episode = episode,
			Streams = 
            {
                new VideoStream
                {
                    Url = url,
					Headers = {[HeaderNames.Referer.ToLower()] = referer },
                }
            }
		};
	}
}
