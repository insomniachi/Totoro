using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeDatabase.Anilist.GraphQl;

namespace Totoro.Modules.AnimeDatabase.Anilist;

internal partial class AnilistMetadataProvider(
	GraphQLHttpClient client,
	IModuleSettings<Settings> settings,
	AnilistLocalList local,
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

		return ToModel(response.Data.Media);
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

			return [.. response.Data.Page.Media.Where(FilterNsfw).Select(ToModel)];
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
			var (with, without) = await SplitAsync(client, request, settings.Value.IncludeNsfw);

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
																		genreIn: with.Genres,
																		genreNotIn: without.Genres,
																		tagIn: with.Tags,
																		tagNotIn: without.Tags,
																		averageScoreGreater: ToAverageScoreBound(request.MinimumScore, -1),
																		averageScoreLesser: ToAverageScoreBound(request.MaximumScore, +1),
																		sort: new List<MediaSort?> { MediaSort.PopularityDesc, MediaSort.ScoreDesc },
																		type: MediaType.Anime), 1, MaxPageSize).Build()
			});

			if (response.Errors?.Length > 0)
			{
				return [];
			}

			return [.. response.Data.Page.Media.Where(FilterNsfw).Select(ToModel)];
		}
		catch (Exception ex) when (ex is not OperationCanceledException && !NetworkErrors.IsConnectionFailure(ex))
		{
			logger.LogWarning(ex, "AniList request failed");
			return [];
		}
	}

	public Task<List<string>> GetGenresAsync() => FetchGenresAndTagsAsync(client, settings.Value.IncludeNsfw);

	// Every AniList tag name, adult ones too, to tell tags from genres in a search; loaded with the genres.
	private static HashSet<string>? s_tagNames;

	/// <summary>The genres, then the tag names (adult tags only with <paramref name="includeNsfw"/>). Also used by its manga.</summary>
	internal static async Task<List<string>> FetchGenresAndTagsAsync(GraphQLHttpClient client, bool includeNsfw)
	{
		var query = new QueryQueryBuilder().WithGenreCollection()
										   .WithMediaTagCollection(new MediaTagQueryBuilder().WithName().WithIsAdult())
										   .Build();

		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = query
		});

		if (response.Errors?.Length > 0 || response.Data?.GenreCollection is null)
		{
			throw new InvalidOperationException($"AniList couldn't load genres: {DescribeErrors(response.Errors)}");
		}

		var tags = response.Data.MediaTagCollection?.Where(x => !string.IsNullOrEmpty(x?.Name)).ToList() ?? [];
		s_tagNames = [.. tags.Select(x => x.Name)];
		return
		[
			.. response.Data.GenreCollection.Where(x => !string.IsNullOrEmpty(x)),
			.. tags.Where(x => includeNsfw || x.IsAdult != true).Select(x => x.Name)
		];
	}

	/// <summary>The tag names, loaded first if the genres haven't been (e.g. the agent searches before the view opened).</summary>
	internal static async Task<IReadOnlySet<string>> GetTagNamesAsync(GraphQLHttpClient client, bool includeNsfw)
	{
		if (s_tagNames is null)
		{
			await FetchGenresAndTagsAsync(client, includeNsfw);
		}

		return s_tagNames!;
	}

	/// <summary>Splits names from the genre list into AniList genres and tags; an empty half is null, so it isn't filtered on.</summary>
	internal static (List<string>? Genres, List<string>? Tags) SplitGenresAndTags(IEnumerable<string>? names, IReadOnlySet<string> tagNames)
	{
		var lookup = (names ?? []).ToLookup(tagNames.Contains);
		List<string> genres = [.. lookup[false]];
		List<string> tags = [.. lookup[true]];
		return (genres.Count > 0 ? genres : null, tags.Count > 0 ? tags : null);
	}

	/// <summary>The request's included and excluded names, split into genres and tags.</summary>
	internal static async Task<((List<string>? Genres, List<string>? Tags) With, (List<string>? Genres, List<string>? Tags) Without)> SplitAsync(
		GraphQLHttpClient client, AdvancedSearchRequest request, bool includeNsfw)
	{
		if (request.IncludedGenres is not { Count: > 0 } && request.ExcludedGenres is not { Count: > 0 })
		{
			return default;
		}

		var tagNames = await GetTagNamesAsync(client, includeNsfw);
		return (SplitGenresAndTags(request.IncludedGenres, tagNames), SplitGenresAndTags(request.ExcludedGenres, tagNames));
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
																						 .WithId()
																						 .WithName(new CharacterNameQueryBuilder()
																									   .WithFull()
																									   .WithNative())
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
				Id = x.Id ?? 0,
				Name = x.Name?.Full ?? "",
				NativeName = x.Name?.Native ?? "",
				Image = Uri.TryCreate(x.Image?.Large, UriKind.Absolute, out var uri) && !IsPlaceholder(uri) ? uri : null,
				Description = x.Description ?? "",
				Gender = x.Gender ?? "",
			})
		];
	}

	public Task<List<ReviewModel>> GetReviewsAsync(long animeId)
	{
		return FetchReviewsAsync(client, (int)animeId);
	}

	/// <summary>An anime's reviews, most helpful first.</summary>
	private static async Task<List<ReviewModel>> FetchReviewsAsync(GraphQLHttpClient client, int anilistId)
	{
		var query = new QueryQueryBuilder().WithMedia(new MediaQueryBuilder()
														  .WithReviews(new ReviewConnectionQueryBuilder()
																		   .WithNodes(new ReviewQueryBuilder()
																					  .WithSummary()
																					  .WithBody(true)
																					  .WithScore()
																					  .WithRating()
																					  .WithRatingAmount()
																					  .WithSiteUrl()
																					  .WithCreatedAt()
																					  .WithUser(new UserQueryBuilder()
																									.WithName()
																									.WithAvatar(new UserAvatarQueryBuilder().WithMedium()))),
																	   sort: new List<ReviewSort?> { ReviewSort.RatingDesc },
																	   perPage: 10),
													  id: anilistId,
													  type: MediaType.Anime).Build();

		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = query
		});

		if (response.Errors?.Length > 0)
		{
			throw new InvalidOperationException($"AniList couldn't load reviews for {anilistId}: {DescribeErrors(response.Errors)}");
		}

		return
		[
			.. (response.Data?.Media?.Reviews?.Nodes ?? []).Where(x => x is not null).Select(x => new ReviewModel(
				nameof(AnimeId.Anilist),
				x.User?.Name ?? "",
				string.IsNullOrEmpty(x.User?.Avatar?.Medium) ? null : x.User.Avatar.Medium,
				x.Summary ?? "",
				x.Body ?? "",
				x.Score,
				x.RatingAmount is > 0 and var votes ? $"{x.Rating ?? 0} of {votes} found it helpful" : null,
				string.IsNullOrEmpty(x.SiteUrl) ? null : x.SiteUrl,
				x.CreatedAt is { } created ? DateTimeOffset.FromUnixTimeSeconds(created) : null))
		];
	}

	/// <summary>AniList's stand-in for a character without a picture (character/large/default.jpg): no image, really.</summary>
	internal static bool IsPlaceholder(Uri image) => image.AbsolutePath.EndsWith("/default.jpg", StringComparison.OrdinalIgnoreCase);

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

			return [.. response.Data.Page.Media.Where(FilterNsfw).Select(ToModel)];
		}
		catch (Exception ex) when (ex is not OperationCanceledException && !NetworkErrors.IsConnectionFailure(ex))
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

			return [.. response.Data.Page.Media.Where(FilterNsfw).Select(ToModel)];
		}
		catch (Exception ex) when (ex is not OperationCanceledException && !NetworkErrors.IsConnectionFailure(ex))
		{
			logger.LogWarning(ex, "AniList request failed");
			return [];
		}
	}

	public async Task<List<AnimeModel>> GetSeasonAnimeAsync(Season season, CancellationToken ct)
	{
		var media = await FetchSeasonAsync(client, season, logger, ct);
		return [.. media.Where(FilterNsfw).Select(ToModel)];
	}

	// Pages of a season, at most: the busiest seasons have about 150 anime.
	private const int MaxSeasonPages = 4;

	/// <summary>
	/// The anime that started airing in the season, most popular first, with their relations (as edges: how each
	/// relates). Also used by the MyAnimeList provider, whose API has no relations in its lists.
	/// </summary>
	internal static async Task<List<Media>> FetchSeasonAsync(GraphQLHttpClient client, Season season, ILogger logger, CancellationToken ct)
	{
		List<Media> media = [];
		try
		{
			for (var page = 1; page <= MaxSeasonPages; page++)
			{
				var response = await client.SendQueryAsync<Query>(new GraphQLRequest
				{
					Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
																 .WithPageInfo(new PageInfoQueryBuilder().WithHasNextPage())
																 .WithMedia(MediaWithRelations(),
																			season: AniListModelToAnimeModelConverter.ConvertSeason(season.SeasonName),
																			seasonYear: season.Year,
																			sort: new List<MediaSort?> { MediaSort.PopularityDesc },
																			type: MediaType.Anime), page, MaxPageSize).Build()
				}, ct);

				if (response.Errors?.Length > 0)
				{
					break;
				}

				media.AddRange(response.Data.Page.Media);
				if (response.Data.Page.PageInfo?.HasNextPage != true)
				{
					break;
				}
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "AniList season request failed");
		}

		return media;
	}

	public async Task<List<AnimeModel>> GetRelationsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
	{
		var media = await FetchWithRelationsAsync(client, ids, byMalId: false, logger, ct);
		return [.. media.Where(FilterNsfw).Select(ToModel)];
	}

	/// <summary>
	/// The anime with these AniList ids (or MyAnimeList ids, <paramref name="byMalId"/>), with their relations, a page at
	/// a time: 400 of them are 8 requests. Also used by the MyAnimeList provider. What came before a failure is kept.
	/// </summary>
	internal static async Task<List<Media>> FetchWithRelationsAsync(GraphQLHttpClient client, IReadOnlyCollection<long> ids, bool byMalId, ILogger logger, CancellationToken ct)
	{
		List<Media> media = [];
		try
		{
			foreach (var chunk in ids.Chunk(MaxPageSize))
			{
				var idList = chunk.Select(x => (int?)x).ToList();
				var response = await client.SendQueryAsync<Query>(new GraphQLRequest
				{
					Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
																 .WithMedia(MediaWithRelations(),
																			idIn: byMalId ? null : idList,
																			idMalIn: byMalId ? idList : null,
																			type: MediaType.Anime), 1, MaxPageSize).Build()
				}, ct);

				if (response.Errors?.Length > 0)
				{
					logger.LogWarning("AniList relations request failed: {Errors}", DescribeErrors(response.Errors));
					break;
				}

				media.AddRange(response.Data.Page.Media);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "AniList relations request failed");
		}

		return media;
	}

	/// <summary>An anime with its relations, as edges: how each relates.</summary>
	private static MediaQueryBuilder MediaWithRelations()
	{
		return MediaQueryBuilder().WithRelations(new MediaConnectionQueryBuilder()
													 .WithEdges(new MediaEdgeQueryBuilder()
																	.WithRelationType()
																	.WithNode(MediaQueryBuilderSimple())));
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
						   .Select(ToModel)
			];
		}
		catch (Exception ex) when (ex is not OperationCanceledException && !NetworkErrors.IsConnectionFailure(ex))
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

	internal static string DescribeErrors(GraphQLError[]? errors)
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
			   .WithScore(ScoreFormat.Point10Decimal)
			   .WithStatus()
			   .WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
			   .WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
			   .WithProgress();
	}

	/// <summary>The anime with the user's entry: AniList's, or while signed out the one kept on this PC.</summary>
	private AnimeModel ToModel(Media media)
	{
		var anime = AniListModelToAnimeModelConverter.ConvertModel(media);
		if (string.IsNullOrEmpty(settings.Value.Token))
		{
			anime.Tracking = local.Get(anime.Id);
		}

		return anime;
	}

	internal static MediaQueryBuilder MediaQueryBuilder()
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
			   .WithTags(new MediaTagQueryBuilder().WithName())
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
			   .WithExternalLinks(new MediaExternalLinkQueryBuilder()
									  .WithSite()
									  .WithUrl()
									  .WithType()
									  .WithLanguage()
									  .WithIcon()
									  .WithColor())
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
			   .WithStatus()
			   .WithPopularity()
			   .WithMeanScore();
	}
}
