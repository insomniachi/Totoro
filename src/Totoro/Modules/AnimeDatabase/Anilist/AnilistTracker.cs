using GraphQL;
using GraphQL.Client.Http;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeDatabase.Anilist.GraphQl;

namespace Totoro.Modules.AnimeDatabase.Anilist;

internal class AnilistTracker(GraphQLHttpClient client, IModuleSettings<Settings> settings) : IAnimeTracker
{
	public Guid Id => AnilistModule.Id;

	public string Name => nameof(AnimeId.Anilist);

	public async Task<List<AnimeModel>> GetUserListAsync(CancellationToken ct)
	{
		if (string.IsNullOrEmpty(settings.Value.Token))
		{
			return [];
		}

		// Failures throw rather than return an empty list, so the page can say AniList is down instead of showing nothing.
		var userName = await FetchUserNameAsync(ct);
		if (string.IsNullOrEmpty(userName))
		{
			return [];
		}

		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = new QueryQueryBuilder().WithMediaListCollection(MediaListCollectionBuilder(), userName: userName, type: MediaType.Anime).Build()
		}, ct);
		ThrowIfFailed(response);

		return
		[
			.. response.Data.MediaListCollection.Lists.SelectMany(x => x.Entries).Select(x => AniListModelToAnimeModelConverter.ConvertModel(x.Media))
		];
	}

	public async Task<bool> RemoveAsync(long id)
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

	public async Task<Tracking> UpdateAsync(long id, Tracking tracking)
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
			mediaListEntryBuilder.WithScore(ScoreFormat.Point10Decimal);
		}

		if (tracking.WatchedEpisodes is not null)
		{
			mediaListEntryBuilder.WithProgress();
		}

		// Left out unless edited, so saving progress doesn't overwrite them.
		var notes = tracking.Changed<TextField>("notes");
		var repeat = tracking.Changed<NumberField>("repeat");
		var isPrivate = tracking.Changed<ToggleField>("private");
		var hidden = tracking.Changed<ToggleField>("hiddenFromStatusLists");

		var query = new MutationQueryBuilder()
					.WithSaveMediaListEntry(mediaListEntryBuilder,
											status: AniListModelToAnimeModelConverter.ConvertListStatus(tracking.Status),
											startedAt: AniListModelToAnimeModelConverter.ConvertDate(tracking.StartDate),
											completedAt: AniListModelToAnimeModelConverter.ConvertDate(tracking.FinishDate),
											// Out of 100, whatever the user's score format.
											scoreRaw: tracking.Score is { } score ? (int)Math.Round(score * 10) : null,
											progress: tracking.WatchedEpisodes,
											mediaId: (int)id,
											// Casts: a null value would still be sent, as null; a null parameter is left out.
											notes: notes is null ? null : (QueryBuilderParameter<string>)notes.Value,
											repeat: repeat is null ? null : (QueryBuilderParameter<int?>)(int)repeat.Value,
											@private: isPrivate is null ? null : (QueryBuilderParameter<bool?>)isPrivate.Value,
											hiddenFromStatusLists: hidden is null ? null : (QueryBuilderParameter<bool?>)hidden.Value)
					.Build();

		var response = await client.SendMutationAsync<Mutation>(new GraphQLRequest
		{
			Query = query
		});

		return AniListModelToAnimeModelConverter.ConvertTracking(response.Data.SaveMediaListEntry) ?? tracking;
	}

	public async Task<TrackingForm> GetFormAsync(long id, CancellationToken ct)
	{
		var query = new QueryQueryBuilder()
					.WithViewer(new UserQueryBuilder().WithMediaListOptions(new MediaListOptionsQueryBuilder().WithScoreFormat()))
					.WithMedia(new MediaQueryBuilder().WithMediaListEntry(new MediaListQueryBuilder()
																			  .WithNotes()
																			  .WithRepeat()
																			  .WithPrivate()
																			  .WithHiddenFromStatusLists()),
							   (int)id,
							   type: MediaType.Anime)
					.Build();

		var response = await client.SendQueryAsync<Query>(new GraphQLRequest { Query = query }, ct);
		ThrowIfFailed(response);

		var entry = response.Data.Media?.MediaListEntry;
		var (max, step, style) = response.Data.Viewer?.MediaListOptions?.ScoreFormat switch
		{
			ScoreFormat.Point100 => (100, 1, ScoreStyle.Number),
			ScoreFormat.Point10Decimal => (10, 0.1, ScoreStyle.Number),
			ScoreFormat.Point5 => (5, 1, ScoreStyle.Stars),
			ScoreFormat.Point3 => (3, 1, ScoreStyle.Smileys),
			_ => (10d, 1d, ScoreStyle.Number),
		};

		return new TrackingForm(max, step, ScoreStyle: style, Fields:
		[
			new NumberField("repeat", "Total rewatches", entry?.Repeat ?? 0),
			new TextField("notes", "Notes", entry?.Notes) { IsMultiline = true },
			new ToggleField("hiddenFromStatusLists", "Hide from status lists", entry?.HiddenFromStatusLists ?? false),
			new ToggleField("private", "Private", entry?.Private ?? false),
		]);
	}

	private async Task<string?> FetchUserNameAsync(CancellationToken ct)
	{
		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = new QueryQueryBuilder().WithViewer(new UserQueryBuilder().WithName()).Build()
		}, ct);
		ThrowIfFailed(response);

		return response.Data.Viewer?.Name;
	}

	// AniList reports some failures as errors in a successful response, with no data.
	private static void ThrowIfFailed(GraphQLResponse<Query> response)
	{
		if (response.Data is null || response.Errors is { Length: > 0 })
		{
			throw new InvalidOperationException($"AniList didn't return your list: {response.Errors?.FirstOrDefault()?.Message ?? "no data"}.");
		}
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
													   .WithBannerImage()
													   .WithEpisodes()
													   .WithStatus()
													   .WithMeanScore()
													   .WithPopularity()
													   .WithDescription(true)
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
																		   .WithScore(ScoreFormat.Point10Decimal)
																		   .WithStatus()
																		   .WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
																		   .WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
																		   .WithProgress()
																		   .WithUpdatedAt()))));
	}
}
