using System.Net.Http.Headers;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using static Totoro.Core.Services.AniList.AniListModelToAnimeModelConverter;

namespace Totoro.Core.Services.AniList;

public class AniListTrackingService : ITrackingService
{
    private readonly GraphQLHttpClient _anilistClient = new("https://graphql.anilist.co/", new NewtonsoftJsonSerializer());
    private string _userName;

    public AniListTrackingService(ILocalSettingsService localSettingsService)
    {
        var token = localSettingsService.ReadSetting<AniListAuthToken>("AniListToken", new());
        SetAccessToken(token?.AccessToken);
    }

    public bool IsAuthenticated { get; private set; }
    public ListServiceType Type => ListServiceType.AniList;

    public void SetAccessToken(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return;
        }

        _anilistClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        IsAuthenticated = true;
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        if (IsAuthenticated)
        {
            return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
            {
                var userName = await GetUserName();

                var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
                {
                    Query = new QueryQueryBuilder().WithMediaListCollection(MediaListCollectionBuilder(), userName: userName, type: MediaType.Anime, status: MediaListStatus.Current).Build(),
                });

                observer.OnNext(response.Data.MediaListCollection.Lists.SelectMany(x => x.Entries).Select(x => ConvertModel(x.Media)));

                response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
                {
                    Query = new QueryQueryBuilder().WithMediaListCollection(MediaListCollectionBuilder(), userName: userName, type: MediaType.Anime, statusNot: MediaListStatus.Current).Build(),
                });

                observer.OnNext(response.Data.MediaListCollection.Lists.SelectMany(x => x.Entries).Select(x => ConvertModel(x.Media)));
                observer.OnCompleted();
            });
        }
        else
        {
            return Observable.Empty<IEnumerable<AnimeModel>>();
        }
    }

    public IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        if (IsAuthenticated)
        {
            return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
            {
                var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
                {
                    Query = new QueryQueryBuilder().WithMediaListCollection(MediaListCollectionBuilder(),
                        userName: await GetUserName(),
                        type: MediaType.Anime,
                        status: MediaListStatus.Current)
                    .Build(),
                });

                observer.OnNext(response.Data.MediaListCollection.Lists.SelectMany(x => x.Entries).Select(x => x.Media).Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel));
                observer.OnCompleted();
            });
        }
        else
        {
            return Observable.Empty<IEnumerable<AnimeModel>>();
        }
    }

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        if (IsAuthenticated)
        {
            return Observable.Create<Tracking>(async observer =>
            {
                var mediaListEntryBuilder = new MediaListQueryBuilder();

                if (tracking.Status is AnimeStatus status)
                {
                    mediaListEntryBuilder.WithStatus();
                }
                if (tracking.StartDate is DateTime)
                {
                    mediaListEntryBuilder.WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields());
                }
                if (tracking.FinishDate is DateTime)
                {
                    mediaListEntryBuilder.WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields());
                }
                if (tracking.Score is int)
                {
                    mediaListEntryBuilder.WithScore();
                }
                if (tracking.WatchedEpisodes is int)
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
                observer.OnCompleted();
            });
        }
        else
        {
            return Observable.Return(tracking);
        }
    }

    public IObservable<bool> Delete(long id)
    {
        return Observable.Create<bool>(async observer =>
        {
            var query = new QueryQueryBuilder().WithMedia(new MediaQueryBuilder()
                .WithMediaListEntry(new MediaListQueryBuilder().WithId()),
                id: (int)id,
                type: MediaType.Anime).Build();

            var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
            {
                Query = query
            });

            var trackingId = response.Data?.Media?.MediaListEntry?.Id;

            if (trackingId is null)
            {
                observer.OnNext(false);
                observer.OnCompleted();
            }

            query = new MutationQueryBuilder()
                .WithDeleteMediaListEntry(new DeletedQueryBuilder().WithAllFields(), id: response.Data.Media.MediaListEntry.Id)
                .Build();

            var mutationResponse = await _anilistClient.SendMutationAsync<Mutation>(new GraphQL.GraphQLRequest
            {
                Query = query
            });

            observer.OnNext(mutationResponse.Data?.DeleteMediaListEntry?.Deleted ?? false);
            observer.OnCompleted();
        });
    }

    private static bool CurrentlyAiringOrFinishedToday(Media media)
    {
        if (media.Status == MediaStatus.Releasing)
        {
            return true;
        }

        if (ConvertDate(media.EndDate) is not DateTime dt)
        {
            return false;
        }

        return DateTime.Today == dt;
    }
    private async ValueTask<string> GetUserName()
    {
        _userName ??= await FetchUserName();
        return _userName;
    }

    private async Task<string> FetchUserName()
    {
        var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
        {
            Query = new QueryQueryBuilder().WithViewer(new UserQueryBuilder().WithName()).Build(),
        });

        return response?.Data?.Viewer?.Name;
    }

    private static MediaListCollectionQueryBuilder MediaListCollectionBuilder()
    {
        return new MediaListCollectionQueryBuilder()
                .WithLists(new MediaListGroupQueryBuilder()
                    .WithEntries(new MediaListQueryBuilder()
                        .WithMedia(new MediaQueryBuilder()
                            .WithId()
                            .WithIdMal()
                            .WithSource()
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
                            .WithNextAiringEpisode(new AiringScheduleQueryBuilder()
                                .WithEpisode()
                                .WithTimeUntilAiring())
                            .WithMediaListEntry(new MediaListQueryBuilder()
                                .WithScore()
                                .WithStatus()
                                .WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
                                .WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
                                .WithProgress()))));
    }

    public async Task<Models.User> GetUser()
    {
        var response = await _anilistClient.SendQueryAsync<Query>(new GraphQL.GraphQLRequest
        {
            Query = new QueryQueryBuilder().WithViewer(new UserQueryBuilder()
                    .WithName()
                    .WithAvatar(new UserAvatarQueryBuilder().WithAllFields()))
                    .Build(),
        });

        return new Models.User
        {
            Name = response.Data.Viewer.Name,
            Image = response.Data.Viewer.Avatar?.Large
        };
    }
}
