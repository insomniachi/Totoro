using Splat;

namespace Totoro.Core.Services;

public class TimestampsService : ITimestampsService, IEnableLogger
{
    private readonly ISettings _settings;
    private readonly HttpClient _httpClient = new();

    public TimestampsService(ISettings settings)
    {
        _settings = settings;
    }

    public async Task<AniSkipResult> GetTimeStamps(long malId, int ep, double duration)
    {
        var url = $"https://api.aniskip.com/v2/skip-times/{malId}/{ep}?types[]=op&types[]=ed&episodeLength={duration}";
        this.Log().Debug("Requesting timestamps : {0}", url);
        var response = await _httpClient.GetAsync(url);
        
        if(!response.IsSuccessStatusCode)
        {
            this.Log().Info($"Timestamps for MalId = {malId}, Ep = {ep}, Duration = {duration} not found");
            return new AniSkipResult { Success = false, Items = Enumerable.Empty<AniSkipResultItem>().ToArray() };
        }

        var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync(stream, AniSkipResultSerializerContext.Default.AniSkipResult);
        this.Log().Info("Timestamps received : {0}", result.Success);
        
        return result;
    }

    public async Task SubmitTimeStamp(long malId, int ep, string skipType, Interval interval, double episodeLength)
    {
        var postData = new Dictionary<string, string>()
        {
            ["skipType"] = skipType,
            ["providerName"] = _settings.DefaultProviderType.ToString(),
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

