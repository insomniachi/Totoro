
using System.Net.Http.Headers;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Totoro.Core.Helpers;
using static Totoro.Core.Services.AniList.AniListModelToAnimeModelConverter;

namespace Totoro.Core.Services.AniList;

public class AnilistService : IAnimeService, IAnilistService
{
    private readonly GraphQLHttpClient _anilistClient = new("https://graphql.anilist.co/", new NewtonsoftJsonSerializer());
    private readonly IAnimeIdService _animeIdService;

    public AnilistService(ILocalSettingsService localSettingsSerivce,
                          IAnimeIdService animeIdService)
    {
        _animeIdService = animeIdService;

        var token = localSettingsSerivce.ReadSetting<AniListAuthToken>("AniListToken", new());
        if (!string.IsNullOrEmpty(token.AccessToken))
        {
            _anilistClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }
    }

    public ListServiceType Type => ListServiceType.AniList;

    public IObservable<IEnumerable<AnimeModel>> GetAiringAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
            {
                Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
                    .WithMedia(MediaQueryBuilder(), type: MediaType.Anime, status: MediaStatus.Releasing), page: 1, perPage: 20).Build()
            });

            observer.OnNext(response.Data.Page.Media.Select(ConvertModel));
            observer.OnCompleted();
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime(string name)
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
            {
                Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
                    .WithMedia(MediaQueryBuilder(), search: name, type: MediaType.Anime), page: 1, perPage: 20).Build()
            });

            observer.OnNext(response.Data.Page.Media.Select(ConvertModel));
            observer.OnCompleted();
        });
    }

    public IObservable<AnimeModel> GetInformation(long id)
    {
        return Observable.Create<AnimeModel>(async observer =>
        {
            var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
            {
                Query = new QueryQueryBuilder().WithMedia(MediaQueryBuilderFull(), id: (int)id, type: MediaType.Anime).Build()
            });

            observer.OnNext(ConvertModel(response.Data.Media));
            observer.OnCompleted();
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetSeasonalAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var current = AnimeHelpers.CurrentSeason();
            var prev = AnimeHelpers.PrevSeason();
            var next = AnimeHelpers.NextSeason();

            try
            {
                foreach (var season in new[] { current, prev, next })
                {
                    var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
                    {
                        Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
                                .WithMedia(MediaQueryBuilder(), season: ConvertSeason(season.SeasonName), seasonYear: season.Year, type: MediaType.Anime), 1, 50).Build()
                    });

                    observer.OnNext(response.Data.Page.Media.Select(ConvertModel));
                }

                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }

            return Disposable.Empty;
        });
    }

    public async Task<string> GetBannerImage(long id)
    {
        var animeId = await _animeIdService.GetId(id);

        var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
        {
            Query = new QueryQueryBuilder().WithMedia(new MediaQueryBuilder()
                .WithBannerImage(), id: (int)animeId.AniList).Build()
        });

        return response.Data.Media.BannerImage;
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

        return response.Data.Media.NextAiringEpisode?.Episode;
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

    private static MediaQueryBuilder MediaQueryBuilder()
    {
        return new MediaQueryBuilder()
            .WithId()
            .WithIdMal()
            .WithTitle(new MediaTitleQueryBuilder()
                .WithEnglish()
                .WithNative()
                .WithRomaji())
            .WithCoverImage(new MediaCoverImageQueryBuilder()
                .WithLarge())
            .WithEpisodes()
            .WithStatus()
            .WithMeanScore()
            .WithPopularity()
            .WithDescription(asHtml: false)
            .WithTrailer(new MediaTrailerQueryBuilder()
                .WithSite()
                .WithThumbnail()
                .WithId())
            .WithGenres()
            .WithStartDate(new FuzzyDateQueryBuilder().WithAllFields())
            .WithEndDate(new FuzzyDateQueryBuilder().WithAllFields())
            .WithSeason()
            .WithSeasonYear()
            .WithBannerImage()
            .WithMediaListEntry(new MediaListQueryBuilder()
                .WithScore()
                .WithStatus()
                .WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
                .WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
                .WithProgress());
    }

    private static MediaQueryBuilder MediaQueryBuilderSimple()
    {
        return new MediaQueryBuilder()
            .WithId()
            .WithIdMal()
            .WithTitle(new MediaTitleQueryBuilder()
                .WithEnglish()
                .WithNative()
                .WithRomaji())
            .WithCoverImage(new MediaCoverImageQueryBuilder()
                .WithLarge())
            .WithType()
            .WithMediaListEntry(new MediaListQueryBuilder()
                .WithScore()
                .WithStatus()
                .WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
                .WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
                .WithProgress())
            .WithStatus();
    }

    private static MediaQueryBuilder MediaQueryBuilderFull()
    {
        return new MediaQueryBuilder()
            .WithId()
            .WithIdMal()
            .WithTitle(new MediaTitleQueryBuilder()
                .WithEnglish()
                .WithNative()
                .WithRomaji())
            .WithCoverImage(new MediaCoverImageQueryBuilder()
                .WithLarge())
            .WithEpisodes()
            .WithStatus()
            .WithMeanScore()
            .WithPopularity()
            .WithDescription(asHtml: false)
            .WithTrailer(new MediaTrailerQueryBuilder()
                .WithSite()
                .WithThumbnail()
                .WithId())
            .WithGenres()
            .WithStartDate(new FuzzyDateQueryBuilder().WithAllFields())
            .WithEndDate(new FuzzyDateQueryBuilder().WithAllFields())
            .WithSeason()
            .WithSeasonYear()
            .WithBannerImage()
            .WithRelations(new MediaConnectionQueryBuilder()
                .WithNodes(MediaQueryBuilderSimple()))
            .WithRecommendations(new RecommendationConnectionQueryBuilder()
                .WithNodes(new RecommendationQueryBuilder()
                    .WithMediaRecommendation(MediaQueryBuilderSimple())))
            .WithMediaListEntry(new MediaListQueryBuilder()
                .WithScore()
                .WithStatus()
                .WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
                .WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
                .WithProgress());
    }
}
