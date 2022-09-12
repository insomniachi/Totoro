using AnimDL.UI.Core.Helpers;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;

namespace AnimDL.UI.Core.Services;

public class TimestampsService : ITimestampsService
{

    private readonly GraphQLHttpClient _anilistClient = new GraphQLHttpClient("https://graphql.anilist.co/", new SystemTextJsonSerializer());
    private readonly GraphQLHttpClient _animeSkipClient = new GraphQLHttpClient("https://api.anime-skip.com/graphql", new SystemTextJsonSerializer());

    public TimestampsService()
    {
        _animeSkipClient.HttpClient.DefaultRequestHeaders.Add("X-Client-ID", "ZGfO0sMF3eCwLYf8yMSCJjlynwNGRXWE");
    }

    public async Task<AnimeTimeStamps> GetTimeStamps(long malId)
    {
        var anilistRequest = new GraphQLRequest
        {
            Query = GraphQLQueries.GetAniListIdFromMal(),
            Variables = new { idMal = malId },
        };

        var mediaResponse = await _anilistClient.SendQueryAsync<MediaResponse>(anilistRequest);
        var anilistId = mediaResponse.Data.Media.Id;

        var animeSkipRequest = new GraphQLRequest
        {
            Query = GraphQLQueries.GetTimeStamps(),
            Variables = new { serviceId = anilistId.ToString() }
        };

        var showResponse = await _animeSkipClient.SendQueryAsync<ShowResponse>(animeSkipRequest);

        var animeTimeStamps = new AnimeTimeStamps();
        foreach (var item in showResponse.Data.Shows[0].Episodes)
        {
            var intro = item.TimeStamps.FirstOrDefault(x => x.Type.Description.ToLower().Contains("intro"))?.Time ?? -1.0;
            var outro = item.TimeStamps.FirstOrDefault(x => x.Type.Description.ToLower().Contains("outro"))?.Time ?? -1.0;
            animeTimeStamps.EpisodeTimeStamps.Add(item.EpisodeNumber, new EpisodeTimeStamp { Intro = intro, Outro = outro });
        }

        return animeTimeStamps;
    }
}

public class AnimeTimeStamps
{
    public Dictionary<string, EpisodeTimeStamp> EpisodeTimeStamps { get; set; } = new();
}

public class EpisodeTimeStamp
{
    public double Intro { get; set; }
    public double Outro { get; set; }
}
