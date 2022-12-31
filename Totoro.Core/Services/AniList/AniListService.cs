
using System.Net.Http.Headers;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Totoro.Core.Helpers;
using static Totoro.Core.Services.AniList.AniListModelToAnimeModelConverter;

namespace Totoro.Core.Services.AniList;

public class AniListService : IAnimeService
{
    private readonly GraphQLHttpClient _anilistClient = new("https://graphql.anilist.co/", new NewtonsoftJsonSerializer());

    public AniListService(ILocalSettingsService localSettingsSerivce)
    {
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
                Query = new QueryQueryBuilder().WithMedia(MediaQueryBuilder(), idMal: (int)id, type: MediaType.Anime).Build()
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
            .WithDescription()
            .WithTrailer(new MediaTrailerQueryBuilder()
                .WithSite()
                .WithThumbnail()
                .WithId())
            .WithGenres()
            .WithStartDate(new FuzzyDateQueryBuilder().WithAllFields())
            .WithEndDate(new FuzzyDateQueryBuilder().WithAllFields())
            .WithSeason()
            .WithSeasonYear()
            .WithMediaListEntry(new MediaListQueryBuilder()
                .WithScore()
                .WithStatus()
                .WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
                .WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
                .WithProgress());
    }
}
