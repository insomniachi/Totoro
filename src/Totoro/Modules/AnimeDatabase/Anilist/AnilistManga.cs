using GraphQL;
using GraphQL.Client.Http;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeDatabase.Anilist.GraphQl;

namespace Totoro.Modules.AnimeDatabase.Anilist;

/// <summary>AniList's manga, as the manga database. Unlike its anime, there's no list kept on this PC: tracking needs an account.</summary>
internal class AnilistMangaMetadataProvider(GraphQLHttpClient client, IModuleSettings<Settings> settings) : IMangaMetadataProvider
{
	public Guid Id => AnilistModule.Id;

	public string Name => nameof(MangaId.Anilist);

	public async Task<MangaModel> GetMangaAsync(long id)
	{
		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = new QueryQueryBuilder().WithMedia(AnilistMangaConverter.MediaBuilder(), (int)id, type: MediaType.Manga).Build()
		});
		AnilistMangaConverter.ThrowIfFailed(response);
		return AnilistMangaConverter.ToModel(response.Data.Media, settings.Value);
	}

	public Task<List<MangaModel>> SearchMangaAsync(string term, CancellationToken ct) =>
		GetPageAsync(search: term, sort: [MediaSort.SearchMatch], ct: ct);

	public Task<List<MangaModel>> GetPopularMangaAsync(CancellationToken ct) =>
		GetPageAsync(sort: [MediaSort.PopularityDesc], ct: ct);

	public Task<List<MangaModel>> GetTrendingMangaAsync(CancellationToken ct) =>
		GetPageAsync(sort: [MediaSort.TrendingDesc, MediaSort.PopularityDesc], ct: ct);

	/// <summary>The same genres as its anime.</summary>
	public async Task<List<string>> GetGenresAsync()
	{
		var response = await client.SendQueryAsync<Query>(new GraphQLRequest { Query = new QueryQueryBuilder().WithGenreCollection().Build() });
		AnilistMangaConverter.ThrowIfFailed(response);
		return [.. response.Data.GenreCollection.Where(x => !string.IsNullOrEmpty(x))];
	}

	public Task<List<MangaModel>> SearchMangaAsync(AdvancedSearchRequest request, CancellationToken ct)
	{
		// Fuzzy dates are yyyymmdd and the bounds exclusive: from 1 Jan of the first year to 31 Dec of the last.
		var from = request.MinYear is > 1950 and var min ? min * 10000 : (int?)null;
		var to = request.MaxYear is > 1950 and var max ? (max + 1) * 10000 : (int?)null;
		return GetPageAsync([MediaSort.PopularityDesc, MediaSort.ScoreDesc], ct,
			search: string.IsNullOrWhiteSpace(request.Title) ? null : request.Title,
			genres: request.IncludedGenres, excludedGenres: request.ExcludedGenres,
			// Out of 100, and exclusive: widened by one to keep the request's bounds.
			minimumScore: request.MinimumScore is { } low ? (int)MathF.Round(low * 10) - 1 : null,
			maximumScore: request.MaximumScore is { } high ? (int)MathF.Round(high * 10) + 1 : null,
			startedAfter: from, startedBefore: to, pageSize: 50);
	}

	private async Task<List<MangaModel>> GetPageAsync(List<MediaSort?> sort, CancellationToken ct, string? search = null,
		List<string>? genres = null, List<string>? excludedGenres = null, int? minimumScore = null, int? maximumScore = null,
		int? startedAfter = null, int? startedBefore = null, int? pageSize = null)
	{
		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = new QueryQueryBuilder().WithPage(new PageQueryBuilder()
				.WithMedia(AnilistMangaConverter.MediaBuilder(), search: search, sort: sort, type: MediaType.Manga,
					isAdult: settings.Value.IncludeNsfw ? null : false,
					genreIn: genres, genreNotIn: excludedGenres,
					averageScoreGreater: minimumScore, averageScoreLesser: maximumScore,
					startDateGreater: startedAfter, startDateLesser: startedBefore),
				1, pageSize ?? (int)settings.Value.SearchLimit).Build()
		}, ct);
		AnilistMangaConverter.ThrowIfFailed(response);
		return [.. response.Data.Page.Media.Select(x => AnilistMangaConverter.ToModel(x, settings.Value))];
	}
}

internal class AnilistMangaTracker(GraphQLHttpClient client, IModuleSettings<Settings> settings) : IMangaTracker
{
	public Guid Id => AnilistModule.Id;

	public string Name => nameof(MangaId.Anilist);

	private void ThrowIfSignedOut()
	{
		if (string.IsNullOrEmpty(settings.Value.Token))
		{
			throw new InvalidOperationException("Sign in to AniList to keep a manga list.");
		}
	}

	public async Task<List<MangaModel>> GetUserListAsync(CancellationToken ct)
	{
		if (string.IsNullOrEmpty(settings.Value.Token))
		{
			return [];
		}

		var viewer = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = new QueryQueryBuilder().WithViewer(new UserQueryBuilder().WithName()).Build()
		}, ct);
		AnilistMangaConverter.ThrowIfFailed(viewer);

		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = new QueryQueryBuilder().WithMediaListCollection(new MediaListCollectionQueryBuilder()
				.WithLists(new MediaListGroupQueryBuilder()
					.WithEntries(new MediaListQueryBuilder().WithMedia(AnilistMangaConverter.MediaBuilder()))),
				userName: viewer.Data.Viewer?.Name, type: MediaType.Manga).Build()
		}, ct);
		AnilistMangaConverter.ThrowIfFailed(response);

		return [.. response.Data.MediaListCollection.Lists.SelectMany(x => x.Entries)
			.Select(x => AnilistMangaConverter.ToModel(x.Media, settings.Value)).DistinctBy(x => x.Id)];
	}

	public async Task<MangaTracking> UpdateAsync(long id, MangaTracking tracking)
	{
		ThrowIfSignedOut();
		var query = new MutationQueryBuilder()
			.WithSaveMediaListEntry(AnilistMangaConverter.EntryBuilder(),
				mediaId: (int)id,
				status: AniListModelToAnimeModelConverter.ConvertListStatus(tracking.Status),
				scoreRaw: tracking.Score is { } score ? (int)Math.Round(score * 10) : null,
				progress: tracking.ChaptersRead,
				progressVolumes: tracking.VolumesRead,
				startedAt: AniListModelToAnimeModelConverter.ConvertDate(tracking.StartDate),
				completedAt: AniListModelToAnimeModelConverter.ConvertDate(tracking.FinishDate))
			.Build();

		var response = await client.SendMutationAsync<Mutation>(new GraphQLRequest { Query = query });
		if (response.Errors is { Length: > 0 } errors)
		{
			throw new InvalidOperationException($"AniList didn't save it: {errors[0].Message}");
		}

		return AnilistMangaConverter.ConvertTracking(response.Data.SaveMediaListEntry) ?? tracking;
	}

	public async Task<bool> RemoveAsync(long id)
	{
		ThrowIfSignedOut();
		var response = await client.SendQueryAsync<Query>(new GraphQLRequest
		{
			Query = new QueryQueryBuilder().WithMedia(new MediaQueryBuilder().WithMediaListEntry(new MediaListQueryBuilder().WithId()),
				(int)id, type: MediaType.Manga).Build()
		});
		if (response.Data?.Media?.MediaListEntry?.Id is not { } entryId)
		{
			return false;
		}

		var deleted = await client.SendMutationAsync<Mutation>(new GraphQLRequest
		{
			Query = new MutationQueryBuilder().WithDeleteMediaListEntry(new DeletedQueryBuilder().WithAllFields(), entryId).Build()
		});
		return deleted.Data?.DeleteMediaListEntry?.Deleted ?? false;
	}
}

internal static class AnilistMangaConverter
{
	public static MediaListQueryBuilder EntryBuilder() => new MediaListQueryBuilder()
		.WithScore(ScoreFormat.Point10Decimal)
		.WithStatus()
		.WithProgress()
		.WithProgressVolumes()
		.WithStartedAt(new FuzzyDateQueryBuilder().WithAllFields())
		.WithCompletedAt(new FuzzyDateQueryBuilder().WithAllFields())
		.WithUpdatedAt();

	public static MediaQueryBuilder MediaBuilder() => new MediaQueryBuilder()
		.WithId()
		.WithIdMal()
		.WithFormat()
		.WithCountryOfOrigin()
		.WithTitle(new MediaTitleQueryBuilder().WithEnglish().WithRomaji().WithNative())
		.WithSynonyms()
		.WithCoverImage(new MediaCoverImageQueryBuilder().WithLarge())
		.WithBannerImage()
		.WithChapters()
		.WithVolumes()
		.WithStatus()
		.WithMeanScore()
		.WithPopularity()
		.WithDescription(true)
		.WithGenres()
		.WithMediaListEntry(EntryBuilder());

	public static MangaModel ToModel(Media media, Settings settings)
	{
		var english = media.Title?.English;
		var romaji = media.Title?.Romaji;
		return new MangaModel
		{
			Id = media.Id ?? 0,
			ExternalIds = new MangaId { Anilist = media.Id ?? 0, MyAnimeList = media.IdMal ?? 0 },
			Title = (settings.TitleLanguage == TitleLanguage.English ? english ?? romaji : romaji ?? english) ?? "",
			EngTitle = english ?? "",
			RomajiTitle = romaji ?? "",
			AlternateTitles = [.. media.Synonyms?.Where(x => !string.IsNullOrEmpty(x)) ?? []],
			Image = media.CoverImage?.Large ?? "",
			BannerImage = media.BannerImage ?? media.CoverImage?.Large ?? "",
			TotalChapters = media.Chapters,
			TotalVolumes = media.Volumes,
			Status = AniListModelToAnimeModelConverter.ConvertStatus(media.Status),
			MeanScore = media.MeanScore,
			Popularity = media.Popularity ?? 0,
			Description = media.Description ?? "",
			Genres = [.. media.Genres ?? []],
			Format = ConvertFormat(media.Format, media.CountryOfOrigin as string),
			Url = $"https://anilist.co/manga/{media.Id}/",
			ServiceId = AnilistModule.Id,
			ServiceName = nameof(MangaId.Anilist),
			Tracking = ConvertTracking(media.MediaListEntry),
		};
	}

	/// <summary>AniList calls comics from Korea and China manga too; their country tells manhwa and manhua apart.</summary>
	public static MangaFormat ConvertFormat(MediaFormat? format, string? country) => format switch
	{
		MediaFormat.Novel => MangaFormat.Novel,
		MediaFormat.OneShot => MangaFormat.OneShot,
		MediaFormat.Manga => country switch
		{
			"KR" => MangaFormat.Manhwa,
			"CN" or "TW" => MangaFormat.Manhua,
			_ => MangaFormat.Manga,
		},
		_ => MangaFormat.Unknown,
	};

	public static MangaTracking? ConvertTracking(MediaList? entry) => entry is null ? null : new MangaTracking
	{
		Status = AniListModelToAnimeModelConverter.ConvertListStatus(entry.Status),
		Score = entry.Score is > 0 and var score ? (double)score : null,
		ChaptersRead = entry.Progress,
		VolumesRead = entry.ProgressVolumes,
		StartDate = AniListModelToAnimeModelConverter.ConvertDate(entry.StartedAt),
		FinishDate = AniListModelToAnimeModelConverter.ConvertDate(entry.CompletedAt),
		UpdatedAt = entry.UpdatedAt is > 0 and var updated ? DateTimeOffset.FromUnixTimeSeconds(updated).LocalDateTime : null,
	};

	// AniList reports some failures as errors in a successful response, with no data.
	public static void ThrowIfFailed(GraphQLResponse<Query> response)
	{
		if (response.Data is null || response.Errors is { Length: > 0 })
		{
			throw new InvalidOperationException($"AniList didn't answer: {response.Errors?.FirstOrDefault()?.Message ?? "no data"}.");
		}
	}
}
