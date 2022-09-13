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

        if(showResponse.Data is null || showResponse.Data.Shows is null || showResponse.Data.Shows.Length == 0)
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

public class AnimeTimeStamps
{
    public Dictionary<string, EpisodeTimeStamp> EpisodeTimeStamps { get; set; } = new();

    public double GetIntroStartPosition(string episode)
    {
        if(EpisodeTimeStamps.ContainsKey(episode))
        {
            return EpisodeTimeStamps[episode].Intro;
        }

        return -1.0;
    }

    public TimeSpan GetIntroEndPosition(string episode)
    {
        return TimeSpan.FromSeconds(GetIntroStartPosition(episode)) + TimeSpan.FromSeconds(89);
    }

    public double GetOutroStartPosition(string episode)
    {
        if (EpisodeTimeStamps.ContainsKey(episode))
        {
            return EpisodeTimeStamps[episode].Outro;
        }

        return -1.0;
    }
}

public class EpisodeTimeStamp
{
    public double Intro { get; set; }
    public double Outro { get; set; }
}
