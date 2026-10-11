using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeDatabase.Anilist.GraphQl;

namespace Totoro.Modules.AnimeDatabase.Anilist;

internal partial class AnilistMetadataProvider(
	GraphQLHttpClient client,
	IModuleSettings<Settings> settings,
	ILogger<AnilistMetadataProvider> logger) : IAnimeMetadataProvider
{
	// AniList allows at most 50 items per page.
	private const int MaxPageSize = 50;

	public Guid Id => AnilistModule.Id;

	public string Name => nameof(AnimeId.Anilist);

	public async Task<AnimeModel> GetAnimeAsync(long id)
	{
		var query = new QueryQueryBuilder().WithMedia(MediaQueryBuilderFull(), (int)id, type: MediaType.Anime).Build();

		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = query
		});

		if (response.Errors?.Length > 0 || response.Data?.Media is null)
		{
			throw new InvalidOperationException($"AniList couldn't load anime {id}: {DescribeErrors(response.Errors)}");
		}

		return AniListModelToAnimeModelConverter.ConvertModel(response.Data.Media);
	}

	public async Task<List<AnimeModel>> SearchAnimeAsync(string term)
	{
		try
		{
			var response = await client.SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
															 .WithMedia(MediaQueryBuilder(), search: term, type: MediaType.Anime), 1,
														 (int)settings.Value.SearchLimit).Build()
			});

			if (response.Errors?.Length > 0)
			{
				return [];
			}

			return [.. response.Data.Page.Media.Where(FilterNsfw).Select(AniListModelToAnimeModelConverter.ConvertModel)];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "AniList request failed");
			return [];
		}
	}

	public async Task<List<AnimeModel>> SearchAnimeAsync(AdvancedSearchRequest request)
	{
		try
		{
			var (startedAfter, startedBefore, seasonYear) = GetDateFilters(request);

			var response = await client.SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
															 .WithMedia(MediaQueryBuilder(),
																		search: string.IsNullOrWhiteSpace(request.Title) ? null : request.Title,
																		season: AniListModelToAnimeModelConverter.ConvertSeason(request.SeasonName),
																		seasonYear: seasonYear,
																		startDateGreater: startedAfter,
																		startDateLesser: startedBefore,
																		source: AniListModelToAnimeModelConverter.ConvertSource(request.Source),
																		genreIn: request.IncludedGenres,
																		genreNotIn: request.ExcludedGenres,
																		averageScoreGreater: ToAverageScoreBound(request.MinimumScore, -1),
																		averageScoreLesser: ToAverageScoreBound(request.MaximumScore, +1),
																		sort: new List<MediaSort?> { MediaSort.PopularityDesc, MediaSort.ScoreDesc },
																		type: MediaType.Anime), 1, MaxPageSize).Build()
			});

			if (response.Errors?.Length > 0)
			{
				return [];
			}

			return [.. response.Data.Page.Media.Where(FilterNsfw).Select(AniListModelToAnimeModelConverter.ConvertModel)];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "AniList request failed");
			return [];
		}
	}

	public async Task<List<string>> GetGenresAsync()
	{
		var query = new QueryQueryBuilder().WithGenreCollection().Build();

		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = query
		});

		if (response.Errors?.Length > 0 || response.Data?.GenreCollection is null)
		{
			throw new InvalidOperationException($"AniList couldn't load genres: {DescribeErrors(response.Errors)}");
		}

		return [.. response.Data.GenreCollection.Where(x => !string.IsNullOrEmpty(x))];
	}

	public Task<List<EpisodeInfo>> GetEpisodesAsync(AnimeModel anime)
	{
		return anime.GetEpisodesAsync();
	}

	public Task<List<CharacterModel>> GetCharactersAsync(long animeId)
	{
		return FetchCharactersAsync(client, anilistId: (int)animeId);
	}

	/// <summary>
	/// An anime's characters, looked up by its AniList or MyAnimeList id. Also used by other databases that have no
	/// character data of their own.
	/// </summary>
	internal static async Task<List<CharacterModel>> FetchCharactersAsync(GraphQLHttpClient client, int? anilistId = null, int? malId = null)
	{
		var animeId = anilistId ?? malId;
		var query = new QueryQueryBuilder().WithMedia(new MediaQueryBuilder()
														  .WithCharacters(new CharacterConnectionQueryBuilder()
																			  .WithNodes(new CharacterQueryBuilder()
																						 .WithName(new CharacterNameQueryBuilder()
																									   .WithFull())
																						 .WithImage(new CharacterImageQueryBuilder()
																										.WithLarge())
																						 .WithDescription(true)
																						 .WithGender())),
													  id: anilistId,
													  idMal: malId,
													  type: MediaType.Anime).Build();

		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = query
		});

		if (response.Errors?.Length > 0)
		{
			throw new InvalidOperationException($"AniList couldn't load characters for {animeId}: {DescribeErrors(response.Errors)}");
		}

		if (response.Data?.Media?.Characters?.Nodes is not { } characters)
		{
			return [];
		}

		return
		[
			.. characters.Where(x => x is not null).Select(x => new CharacterModel
			{
				Name = x.Name?.Full ?? "",
				Image = Uri.TryCreate(x.Image?.Large, UriKind.Absolute, out var uri) && !IsPlaceholder(uri) ? uri : null,
				Description = x.Description ?? "",
				Gender = x.Gender ?? "",
			})
		];
	}

	/// <summary>AniList's stand-in for a character without a picture (character/large/default.jpg): no image, really.</summary>
	private static bool IsPlaceholder(Uri image) => image.AbsolutePath.EndsWith("/default.jpg", StringComparison.OrdinalIgnoreCase);

	public async Task<List<AnimeModel>> GetPopularAnimeAsync(CancellationToken ct)
	{
		try
		{
			var response = await client.SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
															 .WithMedia(MediaQueryBuilder(),
																		sort: new List<MediaSort?> { MediaSort.TrendingDesc, MediaSort.PopularityDesc },
																		status: MediaStatus.Releasing,
																		countryOfOrigin: "JP",
																		type: MediaType.Anime), 1,
														 (int)settings.Value.SearchLimit).Build()
			}, ct);

			if (response.Errors?.Length > 0)
			{
				return [];
			}

			return [.. response.Data.Page.Media.Where(FilterNsfw).Select(AniListModelToAnimeModelConverter.ConvertModel)];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "AniList request failed");
			return [];
		}
	}

	public async Task<List<AnimeModel>> GetUpcomingAnimeAsync(CancellationToken ct)
	{
		try
		{
			var response = await client.SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
															 .WithMedia(MediaQueryBuilder(),
																		sort: new List<MediaSort?> { MediaSort.PopularityDesc },
																		status: MediaStatus.NotYetReleased,
																		type: MediaType.Anime), 1,
														 (int)settings.Value.SearchLimit).Build()
			}, ct);

			if (response.Errors?.Length > 0)
			{
				return [];
			}

			return [.. response.Data.Page.Media.Where(FilterNsfw).Select(AniListModelToAnimeModelConverter.ConvertModel)];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "AniList request failed");
			return [];
		}
	}

	public async Task<List<AnimeModel>> GetAiringTodayAsync(CancellationToken ct)
	{
		try
		{
			// The user's local day, not the UTC one: e.g. at UTC+5:30 a UTC "today" runs from 05:30 to 05:30.
			var today = new DateTimeOffset(DateTime.Today);
			var start = (int)today.ToUnixTimeSeconds();
			var end = (int)today.AddDays(1).ToUnixTimeSeconds();
			var response = await client.SendQueryAsync<Query>(new GraphQLRequest
			{
				Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
															 .WithAiringSchedules(new AiringScheduleQueryBuilder()
																					  .WithMedia(MediaQueryBuilder()),
																				  airingAtGreater: start,
																				  airingAtLesser: end,
																				  sort: new List<AiringSort?> { AiringSort.Time }), 1, MaxPageSize)
											   .Build()
			}, ct);

			if (response.Errors?.Length > 0)
			{
				return [];
			}

			// A show airing two episodes in a day has two schedule entries; list it once.
			return
			[
				.. response.Data.Page.AiringSchedules
						   .Select(x => x.Media)
						   .Where(x => x is not null)
						   .DistinctBy(x => x.Id)
						   .Where(FilterNsfw)
						   .Select(AniListModelToAnimeModelConverter.ConvertModel)
			];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "AniList request failed");
			return [];
		}
	}

	/// <summary>
	/// Turns the request's year range into AniList start-date filters (or a season year for a single year).
	/// Filters on when a show started, at both ends, so shows that are still airing (no end date) aren't dropped.
	/// </summary>
	private static (int? StartedAfter, int? StartedBefore, int? SeasonYear) GetDateFilters(AdvancedSearchRequest request)
	{
		var minYear = request.MinYear is > 1950 ? request.MinYear : null;
		var maxYear = request.MaxYear is > 1950 ? request.MaxYear : null;

		if (minYear is not null && minYear == maxYear)
		{
			return (null, null, minYear);
		}

		// AniList fuzzy dates are integers in yyyymmdd form, and the filters are exclusive:
		// "started after yyyy0000" includes 1 Jan of minYear, "started before (maxYear+1)0000" includes 31 Dec of maxYear.
		return (minYear * 10000, (maxYear + 1) * 10000, null);
	}

	/// <summary>
	/// Converts a 0-10 score from the request to AniList's 0-100 average score. AniList's filters are exclusive, so
	/// the bound is widened by one (<paramref name="widenBy"/>) to keep the request's bounds inclusive.
	/// </summary>
	private static int? ToAverageScoreBound(float? score, int widenBy)
	{
		return score is { } s ? (int)MathF.Round(s * 10) + widenBy : null;
	}

	private static string DescribeErrors(GraphQLError[]? errors)
	{
		return errors is { Length: > 0 } ? string.Join("; ", errors.Select(x => x.Message)) : "no data returned";
	}

	private bool FilterNsfw(Media m)
	{
		return settings.Value.IncludeNsfw || m.IsAdult is false or null;
	}

	private static MediaListQueryBuilder MediaListEntryQueryBuilder()
	{
		// POINT_10 so scores match what AnilistTracker.Update writes (scoreRaw = Score * 10).
		return new MediaListQueryBuilder()
			   .WithScore(ScoreFormat.Point10)
			   .WithStatus()
			   .WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
			   .WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
			   .WithProgress();
	}

	private static MediaQueryBuilder MediaQueryBuilder()
	{
		return new MediaQueryBuilder()
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
			   .WithBannerImage()
			   .WithMediaListEntry(MediaListEntryQueryBuilder())
			   .WithIsAdult();
	}

	private static MediaQueryBuilder MediaQueryBuilderFull()
	{
		// A superset of what the user list loads (see AnilistTracker), since the details page replaces the list's
		// model with this one: anything missing here (format, next episode...) would disappear once it loads.
		return new MediaQueryBuilder()
			   .WithId()
			   .WithIdMal()
			   .WithFormat()
			   .WithNextAiringEpisode(new AiringScheduleQueryBuilder()
										  .WithEpisode()
										  .WithTimeUntilAiring())
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
			   .WithBannerImage()
			   .WithStudios(new StudioConnectionQueryBuilder()
								.WithNodes(new StudioQueryBuilder()
										   .WithName()
										   .WithIsAnimationStudio()))
			   // Edges rather than nodes: they say how each relates (sequel, prequel...), which finding seasons needs.
			   .WithRelations(new MediaConnectionQueryBuilder()
								  .WithEdges(new MediaEdgeQueryBuilder()
												 .WithRelationType()
												 .WithNode(MediaQueryBuilderSimple())))
			   .WithRecommendations(new RecommendationConnectionQueryBuilder()
										.WithNodes(new RecommendationQueryBuilder()
													   .WithMediaRecommendation(MediaQueryBuilderSimple())))
			   .WithStreamingEpisodes(new MediaStreamingEpisodeQueryBuilder()
									  .WithTitle()
									  .WithThumbnail())
			   .WithAiringSchedule(new AiringScheduleConnectionQueryBuilder()
									   .WithNodes(new AiringScheduleQueryBuilder()
												  .WithEpisode()
												  .WithAiringAt()))
			   .WithMediaListEntry(MediaListEntryQueryBuilder());
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
			   .WithDescription(true)
			   .WithCoverImage(new MediaCoverImageQueryBuilder()
								   .WithLarge())
			   .WithType()
			   .WithFormat()
			   .WithEpisodes()
			   .WithSeason()
			   .WithSeasonYear()
			   .WithMediaListEntry(MediaListEntryQueryBuilder())
			   .WithStatus();
	}
}
