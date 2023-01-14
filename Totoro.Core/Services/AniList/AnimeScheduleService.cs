using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

namespace Totoro.Core.Services.AniList;

internal class AnimeScheduleService : IAnimeScheduleService
{
    private readonly IAnimeIdService _animeIdService;
    private readonly GraphQLHttpClient _anilistClient = new("https://graphql.anilist.co/", new NewtonsoftJsonSerializer());

    public AnimeScheduleService(IAnimeIdService animeIdService)
    {
        _animeIdService = animeIdService;
    }

    public async Task<int?> GetNextAiringEpisode(long id)
    {
        var animeId = await _animeIdService.GetId(id);

        var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
        {
            Query = new QueryQueryBuilder().WithMedia(new MediaQueryBuilder()
                .WithNextAiringEpisode(new AiringScheduleQueryBuilder()
                    .WithEpisode()), id: (int)animeId.AniList).Build()
        });

        return response.Data.Media.NextAiringEpisode.Episode;
    }

    public async Task<DateTime?> GetNextAiringEpisodeTime(long id)
    {
        var animeId = await _animeIdService.GetId(id);

        var response = _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
        {
            Query = new QueryQueryBuilder().WithMedia(new MediaQueryBuilder()
                .WithNextAiringEpisode(new AiringScheduleQueryBuilder()
                    .WithTimeUntilAiring()), id: (int)animeId.AniList).Build()
        });


        var nextEp = response.Result.Data.Media.NextAiringEpisode.TimeUntilAiring;
        DateTime? dt = nextEp is null ? null : DateTime.Now + TimeSpan.FromSeconds(nextEp.Value);

        return dt;
    }
}
