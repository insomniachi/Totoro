using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;

namespace Totoro.Core.Services.Anizip;

public class AnizipEpisodeInfoProvider(TimeProvider timeProvider) : IEpisodesInfoProvider
{
    private readonly string _baseUrl = @"https://api.ani.zip/mappings";
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _today = timeProvider.GetUtcNow();

    public async IAsyncEnumerable<EpisodeInfo> GetEpisodeInfos(long id, string serviceType)
    {
        var response = await _baseUrl.SetQueryParam(serviceType, id).GetStringAsync();
        var jObject = (JsonObject)JsonNode.Parse(response);
        var episodesObj = jObject["episodes"].AsObject();

        foreach (var property in episodesObj)
        {
            var model =  property.Value.Deserialize<EpisodeInfo>();

            if(model.AirDateUtc is null || model.AirDateUtc > _today)
            {
                continue;
            }

            yield return model;
        }
    }
}
