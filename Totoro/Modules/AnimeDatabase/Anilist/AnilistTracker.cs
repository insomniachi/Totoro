using GraphQL;
using GraphQL.Client.Http;
using Totoro.Core.Module.Anime;
using TotoroNext.Anime.Anilist;

namespace Totoro.Modules.AnimeDatabase.Anilist
{
	internal class AnilistTracker(GraphQLHttpClient client) : IAnimeTracker
	{
		public Guid Id { get; }

		public string Name => nameof(AnimeId.Anilist);

		public async Task<List<AnimeModel>> GetUserList(CancellationToken ct)
		{
			string? userName;
			try
			{
				userName = await FetchUserName(ct);
			}
			catch
			{
				return [];
			}

			if (string.IsNullOrEmpty(userName))
			{
				return [];
			}

			var response = await client.SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = new QueryQueryBuilder().WithMediaListCollection(MediaListCollectionBuilder(), userName: userName, type: MediaType.Anime).Build()
			}, ct);


			return
			[
				.. response.Data.MediaListCollection.Lists.SelectMany(x => x.Entries).Select(x => AniListModelToAnimeModelConverter.ConvertModel(x.Media))
			];
		}

		public async Task<bool> Remove(long id)
		{
			var query = new QueryQueryBuilder().WithMedia(new MediaQueryBuilder()
															  .WithMediaListEntry(new MediaListQueryBuilder().WithId()),
														  (int)id,
														  type: MediaType.Anime).Build();

			var response = await client.SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = query
			});

			if (response is { Data.Media.MediaListEntry: null } or null)
			{
				return false;
			}

			var trackingId = response.Data.Media.MediaListEntry.Id;

			if (trackingId is null)
			{
				return false;
			}

			query = new MutationQueryBuilder()
					.WithDeleteMediaListEntry(new DeletedQueryBuilder().WithAllFields(), trackingId)
					.Build();

			var mutationResponse = await client.SendMutationAsync<Mutation>(new GraphQLRequest
			{
				Query = query
			});

			return mutationResponse.Data.DeleteMediaListEntry?.Deleted ?? false;
		}

		public async Task<Tracking> Update(long id, Tracking tracking)
		{
			var mediaListEntryBuilder = new MediaListQueryBuilder();

			mediaListEntryBuilder.WithNotes();

			if (tracking.Status is not null)
			{
				mediaListEntryBuilder.WithStatus();
			}

			if (tracking.StartDate is not null)
			{
				mediaListEntryBuilder.WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields());
			}

			if (tracking.FinishDate is not null)
			{
				mediaListEntryBuilder.WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields());
			}

			if (tracking.Score is not null)
			{
				mediaListEntryBuilder.WithScore();
			}

			if (tracking.WatchedEpisodes is not null)
			{
				mediaListEntryBuilder.WithProgress();
			}

			var query = new MutationQueryBuilder()
						.WithSaveMediaListEntry(mediaListEntryBuilder,
												status: AniListModelToAnimeModelConverter.ConvertListStatus(tracking.Status),
												startedAt: AniListModelToAnimeModelConverter.ConvertDate(tracking.StartDate),
												completedAt: AniListModelToAnimeModelConverter.ConvertDate(tracking.FinishDate),
												scoreRaw: tracking.Score * 10,
												progress: tracking.WatchedEpisodes,
												mediaId: (int)id,
												notes: "#Totoro")
						.Build();

			var response = await client.SendMutationAsync<Mutation>(new GraphQLRequest
			{
				Query = query
			});

			return AniListModelToAnimeModelConverter.ConvertTracking(response.Data.SaveMediaListEntry) ?? tracking;
		}

		private async Task<string?> FetchUserName(CancellationToken ct)
		{
			var response = await client.SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = new QueryQueryBuilder().WithViewer(new UserQueryBuilder().WithName()).Build()
			}, ct);

			return response.Data.Viewer?.Name;
		}

		private static MediaListCollectionQueryBuilder MediaListCollectionBuilder()
		{
			return new MediaListCollectionQueryBuilder()
				.WithLists(new MediaListGroupQueryBuilder()
							   .WithEntries(new MediaListQueryBuilder()
												.WithMedia(new MediaQueryBuilder()
														   .WithId()
														   .WithIdMal()
														   .WithFormat()
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
														   .WithStreamingEpisodes(new MediaStreamingEpisodeQueryBuilder()
																				  .WithTitle()
																				  .WithThumbnail())
														   .WithAiringSchedule(new AiringScheduleConnectionQueryBuilder()
																				   .WithNodes(new AiringScheduleQueryBuilder()
																							  .WithEpisode()
																							  .WithAiringAt()))
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
	}
}
