using System.Net.Http.Headers;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using static Totoro.Core.Services.AniList.AniListModelToAnimeModelConverter;

namespace Totoro.Core.Services.AniList;

public class AniListTrackingService : ITrackingService
{
    private readonly GraphQLHttpClient _anilistClient = new("https://graphql.anilist.co/", new NewtonsoftJsonSerializer());
    private int? _userId;

    public AniListTrackingService(ILocalSettingsService localSettingsService)
    {
        var token = localSettingsService.ReadSetting<AniListAuthToken>("AniListToken", new());
        if(!string.IsNullOrEmpty(token.AccessToken))
        {
            _anilistClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            IsAuthenticated = true;
        }
    }

    public bool IsAuthenticated { get; private set; }
    public ListServiceType Type => ListServiceType.AniList;

    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var userId = await GetUserId();

            var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
            {
                Query = new QueryQueryBuilder().WithMediaListCollection(MediaListCollectionBuilder(), userId: userId, type: MediaType.Anime, status: MediaListStatus.Current).Build(),
            });
            
            observer.OnNext(response.Data.MediaListCollection.Lists.SelectMany(x => x.Entries).Select(x => ConvertModel(x.Media)));

            response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
            {
                Query = new QueryQueryBuilder().WithMediaListCollection(MediaListCollectionBuilder(), userId: userId, type: MediaType.Anime).Build(),
            });
            
            observer.OnNext(response.Data.MediaListCollection.Lists.SelectMany(x => x.Entries).Select(x => ConvertModel(x.Media)));
            
            observer.OnCompleted();
        });
    }

    public IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
            {
                Query = new QueryQueryBuilder().WithMediaListCollection(MediaListCollectionBuilder(), 
                    userId: await GetUserId(),
                    type: MediaType.Anime,
                    status: MediaListStatus.Current)
                .Build(),
            });

            observer.OnNext(response.Data.MediaListCollection.Lists.ElementAt(0).Entries.Select(x => x.Media).Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel));

            observer.OnCompleted();
        });
    }

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        return Observable.Create<Tracking>(async observer =>
        {
            var mediaListEntryBuilder = new MediaListQueryBuilder();

            if(tracking.Status is AnimeStatus status)
            {
                mediaListEntryBuilder.WithStatus();
            }
            if(tracking.StartDate is DateTime)
            {
                mediaListEntryBuilder.WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields());
            }
            if(tracking.FinishDate is DateTime)
            {
                mediaListEntryBuilder.WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields());
            }
            if(tracking.Score is int)
            {
                mediaListEntryBuilder.WithScore();
            }
            if(tracking.WatchedEpisodes is int)
            {
                mediaListEntryBuilder.WithProgress();
            }

            var query = new MutationQueryBuilder()
                .WithSaveMediaListEntry(mediaListEntryBuilder,
                    status: ConvertListStatus(tracking.Status),
                    startedAt: ConvertDate(tracking.StartDate),
                    completedAt: ConvertDate(tracking.FinishDate),
                    scoreRaw: tracking.Score * 100,
                    progress: tracking.WatchedEpisodes,
                    mediaId: (int)id)
                .Build();

            var response = await _anilistClient.SendMutationAsync<Mutation>(new GraphQL.GraphQLRequest
            {
                Query = query
            });

            observer.OnNext(ConvertTracking(response.Data.SaveMediaListEntry));
        });
     }

    private static bool CurrentlyAiringOrFinishedToday(Media media)
    {
        if(media.Status == MediaStatus.Releasing)
        {
            return true;
        }

        if (ConvertDate(media.EndDate) is not DateTime dt)
        {
            return false;
        }

        return DateTime.Today == dt;
    }
    private async ValueTask<int?> GetUserId()
    {
        _userId ??= await FetchUserId();
        return _userId;
    }

    public async Task<int> FetchUserId()
    {
        var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
        {
            Query = new QueryQueryBuilder().WithViewer(new UserQueryBuilder().WithId()).Build(),
        });

        return response?.Data?.Viewer?.Id ?? 0;
    }

    private static MediaListCollectionQueryBuilder MediaListCollectionBuilder()
    {
        return new MediaListCollectionQueryBuilder()
                .WithLists(new MediaListGroupQueryBuilder()
                    .WithEntries(new MediaListQueryBuilder()
                        .WithMedia(new MediaQueryBuilder()
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
                                .WithProgress()))));
    }
}
