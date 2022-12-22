using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Splat;
using Totoro.Core.Helpers;

namespace Totoro.Core.Services;

public class TimestampsService : ITimestampsService, IEnableLogger
{
    private readonly GraphQLHttpClient _animeSkipClient = new("https://api.anime-skip.com/graphql", new SystemTextJsonSerializer());
    private readonly IAnimeIdService _animeIdService;
    private readonly ISettings _settings;
    private readonly Dictionary<long, List<OfflineEpisodeTimeStamp>> _offlineTimestamps;
    private readonly HttpClient _httpClient = new();

    public TimestampsService(IAnimeIdService animeIdService,
                             IFileService fileService,
                             ISettings settings)
    {
        _animeIdService = animeIdService;
        _settings = settings;
        _animeSkipClient.HttpClient.DefaultRequestHeaders.Add("X-Client-ID", "ZGfO0sMF3eCwLYf8yMSCJjlynwNGRXWE");
        _offlineTimestamps = fileService.Read<Dictionary<long, List<OfflineEpisodeTimeStamp>>>("", "timestamps_generated.json");
    }

    // This should be removed if aniskip implementation is better.
    public async Task<AnimeTimeStamps> GetTimeStamps(long malId)
    {
        var animeTimeStamps = new AnimeTimeStamps();

        try
        {
            var id = await _animeIdService.GetId(AnimeTrackerType.MyAnimeList, malId);
            if (_offlineTimestamps.TryGetValue(id.AniDb, out List<OfflineEpisodeTimeStamp> value))
            {
                foreach (var item in value)
                {
                    animeTimeStamps.EpisodeTimeStamps.Add(item.Episode.ToString(), item);
                }

                return animeTimeStamps;
            }
            else
            {
                var animeSkipRequest = new GraphQLRequest
                {
                    Query = GraphQLQueries.GetTimeStamps(),
                    Variables = new { serviceId = id.AniList.ToString() }
                };

                var showResponse = await _animeSkipClient.SendQueryAsync<ShowResponse>(animeSkipRequest);

                if(showResponse is not { Data.Shows.Length: > 0})
                {
                    return animeTimeStamps;
                }

                foreach (var item in showResponse.Data.Shows[0].Episodes)
                {
                    try
                    {
                        var intro = item.TimeStamps.FirstOrDefault(x => x.Type.Description.ToLower().Contains("intro"))?.Time ?? -1.0;
                        var outro = item.TimeStamps.FirstOrDefault(x => x.Type.Description.ToLower().Contains("outro"))?.Time ?? -1.0;
                        animeTimeStamps.EpisodeTimeStamps.Add(item.EpisodeNumber, new EpisodeTimeStamp { Intro = intro, Outro = outro });
                    }
                    catch { }
                }

                return animeTimeStamps;
            }
        }
        catch { }
        return animeTimeStamps;
    }

    public async Task<AniSkipResult> GetTimeStamps(long malId, int ep, double duration)
    {
        var url = $"https://api.aniskip.com/v2/skip-times/{malId}/{ep}?types[]=op&types[]=ed&episodeLength={duration}"; // v1 seems more reliable than v2 now.
        this.Log().Debug("Requesting timestamps : {0}", url);
        try
        {
            var stream = await _httpClient.GetStreamAsync(url);
            var result = await JsonSerializer.DeserializeAsync(stream, AniSkipResultSerializerContext.Default.AniSkipResult);
            this.Log().Info("Timestamps received {0}", result.Success);
            return result;
        }
        catch(Exception ex) 
        {
            this.Log().Error(ex, "Timestamps not received");
            return new AniSkipResult { Success = false, Items = Enumerable.Empty<AniSkipResultItem>().ToArray() };
        }
    }

    public async Task SubmitTimeStamp(long malId, int ep, string skipType, Interval interval, double episodeLength)
    {
        var postData = new Dictionary<string, string>()
        {
            ["skipType"] = skipType,
            ["providerName"] = "AnimixPlay",
            ["startTime"] = interval.StartTime.ToString(),
            ["endTime"] = interval.EndTime.ToString(),
            ["episodeLength"] = episodeLength.ToString(),
            ["submitterId"] = _settings.AniSkipId.ToString()
        };

        this.Log().Info($"Submitting timestamp for MalID: {malId}, Ep: {ep}, Type: {skipType}, Start: {interval.StartTime}, End: {interval.EndTime}, Length: {episodeLength}");

        using var content = new FormUrlEncodedContent(postData);
        content.Headers.Clear();
        content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.aniskip.com/v2/skip-times/{malId}/{ep}");
        request.Content = content;
        var response = await _httpClient.SendAsync(request);
        this.Log().Info("Submitted : {0}", response.IsSuccessStatusCode);
    }
}

public class AnimeTimeStamps
{
    public Dictionary<string, EpisodeTimeStamp> EpisodeTimeStamps { get; set; } = new();

    public double GetIntroStartPosition(string episode)
    {
        if (EpisodeTimeStamps.TryGetValue(episode, out EpisodeTimeStamp value))
        {
            return value.Intro;
        }

        return -1.0;
    }

    public TimeSpan GetIntroEndPosition(string episode)
    {
        return TimeSpan.FromSeconds(GetIntroStartPosition(episode)) + TimeSpan.FromSeconds(89);
    }

    public double GetOutroStartPosition(string episode)
    {
        if (EpisodeTimeStamps.TryGetValue(episode, out EpisodeTimeStamp value))
        {
            return value.Outro;
        }

        return -1.0;
    }
}

[ExcludeFromCodeCoverage]
public class EpisodeTimeStamp
{

    [JsonPropertyName("opening_start")]
    public double Intro { get; set; }

    [JsonPropertyName("ending_start")]
    public double Outro { get; set; }
}

[ExcludeFromCodeCoverage]
public class OfflineEpisodeTimeStamp : EpisodeTimeStamp
{
    [JsonPropertyName("episode_number")]
    public int Episode { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; }
}

