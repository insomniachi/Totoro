using System.Net;
using Flurl;
using Flurl.Http;
using JikanDotNet;
using MalApi;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Manga = MalApi.Manga;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

/// <summary>MyAnimeList's manga, as the manga database.</summary>
internal class MyAnimeListMangaMetadataProvider(MyAnimeListSession session) : IMangaMetadataProvider
{
	public Guid Id => MyAnimeListModule.Id;

	public string Name => nameof(MangaId.MyAnimeList);

	public async Task<MangaModel> GetMangaAsync(long id)
	{
		var client = await session.GetClientAsync();
		return MalMangaConverter.ToModel(await client.Manga().WithId(id).WithFields(MalMangaConverter.Fields).Find());
	}

	public async Task<List<MangaModel>> SearchMangaAsync(string term, CancellationToken ct)
	{
		var client = await session.GetClientAsync();
		var request = client.Manga().WithName(term).WithFields(MalMangaConverter.Fields).WithLimit(session.SearchLimit);
		if (session.IncludeNsfw)
		{
			request.IncludeNsfw();
		}

		return [.. (await request.Find()).Data.Select(MalMangaConverter.ToModel)];
	}

	public Task<List<MangaModel>> GetPopularMangaAsync(CancellationToken ct) => GetRankingAsync(MangaRankingType.ByPopularity);

	// MAL has no trending ranking; its top-rated stands in.
	public Task<List<MangaModel>> GetTrendingMangaAsync(CancellationToken ct) => GetRankingAsync(MangaRankingType.All);

	private async Task<List<MangaModel>> GetRankingAsync(MangaRankingType type)
	{
		var client = await session.GetClientAsync();
		var ranked = await client.Manga().Top(type).WithFields(MalMangaConverter.Fields).WithLimit(session.SearchLimit).Find();
		return [.. ranked.Data.Select(x => MalMangaConverter.ToModel(x.Manga))];
	}

	private static readonly Jikan s_jikan = new();

	// Jikan filters genres by id; loaded on first use.
	private static List<JikanDotNet.Genre>? s_genres;

	private static async Task<List<JikanDotNet.Genre>> LoadGenresAsync() =>
		s_genres ??= (await s_jikan.GetMangaGenresAsync()).Data?.ToList() ?? [];

	public async Task<List<string>> GetGenresAsync() => [.. (await LoadGenresAsync()).Select(x => x.Name).Distinct().Order()];

	/// <summary>Through Jikan (an unofficial MAL API), as MAL's own API has no filters; its results have no list entry.</summary>
	public async Task<List<MangaModel>> SearchMangaAsync(AdvancedSearchRequest request, CancellationToken ct)
	{
		var url = new Url("https://api.jikan.moe/v4/manga");
		if (!string.IsNullOrWhiteSpace(request.Title))
		{
			url.AppendQueryParam("q", request.Title);
		}

		// On MAL's 0-10 scale and inclusive, like the request's.
		if (request.MinimumScore is { } minScore)
		{
			url.AppendQueryParam("min_score", minScore);
		}

		if (request.MaximumScore is { } maxScore)
		{
			url.AppendQueryParam("max_score", maxScore);
		}

		if (request.MinYear is > 1950 and var minYear)
		{
			url.AppendQueryParam("start_date", $"{minYear}-01-01");
		}

		if (request.MaxYear is > 1950 and var maxYear)
		{
			url.AppendQueryParam("end_date", $"{maxYear}-12-31");
		}

		var genres = await LoadGenresAsync();
		string Ids(IEnumerable<string> names) => string.Join(",", names.Select(name => genres.FirstOrDefault(g => g.Name == name)?.MalId).OfType<long>());
		if (request.IncludedGenres is { Count: > 0 } included)
		{
			url.AppendQueryParam("genres", Ids(included));
		}

		if (request.ExcludedGenres is { Count: > 0 } excluded)
		{
			url.AppendQueryParam("genres_exclude", Ids(excluded));
		}

		if (!session.IncludeNsfw)
		{
			url.AppendQueryParam("sfw", true);
		}

		url.AppendQueryParam("order_by", "members").AppendQueryParam("sort", "desc");
		var response = await url.GetJsonAsync<PaginatedJikanResponse<ICollection<JikanDotNet.Manga>>>(cancellationToken: ct);
		return [.. response.Data.Select(MalMangaConverter.FromJikan)];
	}
}

internal class MyAnimeListMangaTracker(MyAnimeListSession session) : IMangaTracker
{
	// MAL's maximum page size for a user's list.
	private const int PageSize = 1000;

	public Guid Id => MyAnimeListModule.Id;

	public string Name => nameof(MangaId.MyAnimeList);

	public async Task<List<MangaModel>> GetUserListAsync(CancellationToken ct)
	{
		if (!session.IsSignedIn)
		{
			return [];
		}

		var client = await session.GetClientAsync();
		List<MangaModel> list = [];
		for (var offset = 0; ; offset += PageSize)
		{
			ct.ThrowIfCancellationRequested();
			var request = client.Manga().OfUser("@me").WithFields(MalMangaConverter.Fields).WithLimit(PageSize).WithOffset(offset);
			if (session.IncludeNsfw)
			{
				request.IncludeNsfw();
			}

			var page = await request.Find();
			list.AddRange(page.Data.Select(MalMangaConverter.ToModel));
			if (string.IsNullOrEmpty(page.Paging?.Next))
			{
				return list;
			}
		}
	}

	/// <summary>Throws if MAL rejects it, so callers don't show a change that didn't happen.</summary>
	public async Task<MangaTracking> UpdateAsync(long id, MangaTracking tracking)
	{
		var client = await session.GetClientAsync();
		var request = client.Manga().WithId(id).UpdateStatus();
		if (tracking.ChaptersRead is { } chapters)
		{
			request.WithChaptersRead(chapters);
		}

		if (tracking.VolumesRead is { } volumes)
		{
			request.WithVolumesRead(volumes);
		}

		switch (tracking.Status)
		{
			// MAL has no rereading status: it's a flag on an entry, which keeps its status.
			case ListItemStatus.Rewatching:
				request.WithIsRereading(true);
				break;
			case { } status:
				request.WithStatus(MalMangaConverter.ConvertStatus(status)).WithIsRereading(false);
				break;
		}

		if (tracking.Score is { } score)
		{
			request.WithScore((Score)Math.Clamp((int)Math.Round(score), 0, 10));
		}

		if (tracking.StartDate is { } start)
		{
			request.WithStartDate(start);
		}

		if (tracking.FinishDate is { } finish)
		{
			request.WithFinishDate(finish);
		}

		return MalMangaConverter.ConvertTracking(await request.Publish()) ?? tracking;
	}

	public async Task<bool> RemoveAsync(long id)
	{
		var client = await session.GetClientAsync();
		return await client.Manga().WithId(id).RemoveFromList();
	}
}

internal static class MalMangaConverter
{
	// Not "status": MalApi reads it as the user's status, which a publishing status isn't.
	public static readonly string[] Fields =
	[
		"synopsis", "mean", "popularity", "alternative_titles", "genres", "media_type", "num_volumes", "num_chapters", "my_list_status",
	];

	public static MangaModel ToModel(Manga manga) => new()
	{
		Id = manga.Id,
		ExternalIds = new MangaId { MyAnimeList = manga.Id },
		Title = manga.Title ?? "",
		EngTitle = manga.AlternativeTitles?.English ?? "",
		AlternateTitles = [.. manga.AlternativeTitles?.Aliases?.Where(x => !string.IsNullOrEmpty(x)) ?? []],
		Image = manga.MainPicture?.Large ?? manga.MainPicture?.Medium ?? "",
		BannerImage = manga.MainPicture?.Large ?? "",
		TotalChapters = manga.TotalChapters > 0 ? manga.TotalChapters : null,
		TotalVolumes = manga.TotalVolumes > 0 ? manga.TotalVolumes : null,
		MeanScore = manga.Mean > 0 ? manga.Mean * 10 : null,
		Popularity = manga.Populartiy,
		// Plain text; kept in paragraphs, as for anime.
		Description = string.IsNullOrEmpty(manga.Synopsis) ? "" : WebUtility.HtmlEncode(manga.Synopsis.Trim()).ReplaceLineEndings("<br>"),
		Genres = [.. manga.Genres?.Select(x => x.Name) ?? []],
		Format = manga.MediaType switch
		{
			MangaMediaType.Manga or MangaMediaType.Doujinshi or MangaMediaType.Oel => MangaFormat.Manga,
			MangaMediaType.Manhwa => MangaFormat.Manhwa,
			MangaMediaType.Manhua => MangaFormat.Manhua,
			MangaMediaType.OneShot => MangaFormat.OneShot,
			MangaMediaType.Novel => MangaFormat.Novel,
			_ => MangaFormat.Unknown,
		},
		Url = $"https://myanimelist.net/manga/{manga.Id}",
		ServiceId = MyAnimeListModule.Id,
		ServiceName = nameof(MangaId.MyAnimeList),
		Tracking = ConvertTracking(manga.UserStatus),
	};

	public static MangaModel FromJikan(JikanDotNet.Manga manga) => new()
	{
		Id = manga.MalId,
		ExternalIds = new MangaId { MyAnimeList = manga.MalId },
		Title = JikanTitle(manga, "Default"),
		EngTitle = JikanTitle(manga, "English"),
		AlternateTitles = [.. manga.Titles?.Where(x => x.Type == "Synonym" && !string.IsNullOrEmpty(x.Title)).Select(x => x.Title) ?? []],
		Image = manga.Images?.JPG?.LargeImageUrl ?? manga.Images?.JPG?.ImageUrl ?? "",
		BannerImage = manga.Images?.JPG?.LargeImageUrl ?? "",
		TotalChapters = manga.Chapters is > 0 ? manga.Chapters : null,
		TotalVolumes = manga.Volumes is > 0 ? manga.Volumes : null,
		MeanScore = manga.Score is > 0 and var score ? (float)score * 10 : null,
		Popularity = manga.Members ?? 0,
		Description = string.IsNullOrEmpty(manga.Synopsis) ? "" : WebUtility.HtmlEncode(manga.Synopsis.Trim()).ReplaceLineEndings("<br>"),
		Genres = [.. manga.Genres?.Select(x => x.Name) ?? []],
		Format = manga.Type switch
		{
			"Manhwa" => MangaFormat.Manhwa,
			"Manhua" => MangaFormat.Manhua,
			"One-shot" => MangaFormat.OneShot,
			"Novel" or "Light Novel" => MangaFormat.Novel,
			_ => MangaFormat.Manga,
		},
		Url = $"https://myanimelist.net/manga/{manga.MalId}",
		ServiceId = MyAnimeListModule.Id,
		ServiceName = nameof(MangaId.MyAnimeList),
	};

	private static string JikanTitle(JikanDotNet.Manga manga, string type) => manga.Titles?.FirstOrDefault(x => x.Type == type)?.Title ?? "";

	public static MangaTracking? ConvertTracking(UserMangaStatus? status) => status is null ? null : new MangaTracking
	{
		ChaptersRead = status.ChaptersRead,
		VolumesRead = status.VolumesRead,
		Status = status.IsReReading ? ListItemStatus.Rewatching : status.Status switch
		{
			MangaStatus.Reading => ListItemStatus.Watching,
			MangaStatus.Completed => ListItemStatus.Completed,
			MangaStatus.OnHold => ListItemStatus.OnHold,
			MangaStatus.PlanToRead => ListItemStatus.PlanToWatch,
			MangaStatus.Dropped => ListItemStatus.Dropped,
			_ => null,
		},
		Score = status.Score > 0 ? status.Score : null,
		StartDate = status.StartDate == default ? null : status.StartDate,
		FinishDate = status.FinishDate == default ? null : status.FinishDate,
		UpdatedAt = status.UpdatedAt == default ? null : status.UpdatedAt,
	};

	/// <summary>Our status as MAL's. Rereading isn't a status on MAL (see MyAnimeListMangaTracker.UpdateAsync).</summary>
	public static MangaStatus ConvertStatus(ListItemStatus status) => status switch
	{
		ListItemStatus.Completed => MangaStatus.Completed,
		ListItemStatus.OnHold => MangaStatus.OnHold,
		ListItemStatus.PlanToWatch => MangaStatus.PlanToRead,
		ListItemStatus.Dropped => MangaStatus.Dropped,
		_ => MangaStatus.Reading,
	};
}
