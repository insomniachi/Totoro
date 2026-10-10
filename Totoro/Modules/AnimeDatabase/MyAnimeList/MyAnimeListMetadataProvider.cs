using Flurl;
using Flurl.Http;
using GraphQL.Client.Http;
using JikanDotNet;
using MalApi;
using Microsoft.Extensions.Logging;
using Totoro.Core.Module.Anime;
using Totoro.Modules.AnimeDatabase.Anilist;
using AdvancedSearchRequest = Totoro.Core.Module.Anime.AdvancedSearchRequest;
using Genre = JikanDotNet.Genre;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

/// <summary>
/// Anime information from MyAnimeList. MAL's own API has no advanced search, schedule or characters, so those come
/// from Jikan (an unofficial MAL API) and AniList (looked up by MAL id).
/// </summary>
internal class MyAnimeListMetadataProvider(
	MyAnimeListSession session,
	GraphQLHttpClient anilistClient,
	ILogger<MyAnimeListMetadataProvider> logger) : IAnimeMetadataProvider
{
	// Fields of the related and recommended anime in the full details.
	private const string NestedFields = $"my_list_status,status,{AnimeFieldNames.TotalEpisodes},{AnimeFieldNames.Mean},{AnimeFieldNames.MediaType}";

	private static readonly Jikan s_jikan = new();

	// Jikan filters genres by id; loaded on first use.
	private static List<Genre>? s_genres;

	public Guid Id => MyAnimeListModule.Id;

	public string Name => nameof(AnimeId.MyAnimeList);

	public async Task<AnimeModel> GetAnimeAsync(long id)
	{
		var client = await session.GetClientAsync();
		var anime = await client.Anime().WithId(id)
								.WithFields(MalToModelConverter.ListFields)
								.WithFields(AnimeFieldNames.Studios)
								.WithFields($"{AnimeFieldNames.RelatedAnime}{{{NestedFields}}}")
								.WithFields($"{AnimeFieldNames.Recommendations}{{{NestedFields}}}")
								.Find();

		return MalToModelConverter.ConvertModel(anime);
	}

	public async Task<List<AnimeModel>> SearchAnimeAsync(string term)
	{
		try
		{
			var client = await session.GetClientAsync();
			var request = client.Anime()
								.WithName(term)
								.WithFields(MalToModelConverter.ListFields)
								.WithLimit(session.SearchLimit);

			if (session.IncludeNsfw)
			{
				request.IncludeNsfw();
			}

			var result = await request.Find();
			return [.. result.Data.Select(MalToModelConverter.ConvertModel)];
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "MyAnimeList search failed");
			return [];
		}
	}

	public async Task<List<AnimeModel>> SearchAnimeAsync(AdvancedSearchRequest request)
	{
		try
		{
			var url = new Url("https://api.jikan.moe/v4/anime");
			if (!string.IsNullOrWhiteSpace(request.Title))
			{
				url.AppendQueryParam("q", request.Title);
			}

			// Jikan's score bounds are inclusive and on MAL's 0-10 scale, like the request's.
			if (request.MinimumScore is { } minScore)
			{
				url.AppendQueryParam("min_score", minScore);
			}

			if (request.MaximumScore is { } maxScore)
			{
				url.AppendQueryParam("max_score", maxScore);
			}

			// Filter on when a show started, at both ends, so shows that are still airing aren't dropped.
			if (request.MinYear is > 1950 and var minYear)
			{
				url.AppendQueryParam("start_date", $"{minYear}-01-01");
			}

			if (request.MaxYear is > 1950 and var maxYear)
			{
				url.AppendQueryParam("end_date", $"{maxYear}-12-31");
			}

			if (request.IncludedGenres is { Count: > 0 } included)
			{
				url.AppendQueryParam("genres", string.Join(",", await GetGenreIdsAsync(included)));
			}

			if (request.ExcludedGenres is { Count: > 0 } excluded)
			{
				url.AppendQueryParam("genres_exclude", string.Join(",", await GetGenreIdsAsync(excluded)));
			}

			if (!session.IncludeNsfw)
			{
				url.AppendQueryParam("sfw", true);
			}

			url.AppendQueryParam("order_by", "members").AppendQueryParam("sort", "desc");

			var response = await url.GetJsonAsync<PaginatedJikanResponse<ICollection<JikanDotNet.Anime>>>();
			return [.. response.Data.Select(MalToModelConverter.ConvertJikanModel)];
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Jikan search failed");
			return [];
		}
	}

	public Task<List<EpisodeInfo>> GetEpisodesAsync(AnimeModel anime)
	{
		return anime.GetEpisodesAsync();
	}

	public Task<List<CharacterModel>> GetCharactersAsync(long animeId)
	{
		return AnilistMetadataProvider.FetchCharactersAsync(anilistClient, malId: (int)animeId);
	}

	public async Task<List<string>> GetGenresAsync()
	{
		return [.. (await LoadGenresAsync()).Select(x => x.Name).Order()];
	}

	public async Task<List<AnimeModel>> GetPopularAnimeAsync(CancellationToken ct)
	{
		return await GetRankingAsync(AnimeRankingType.Airing, ct);
	}

	public async Task<List<AnimeModel>> GetUpcomingAnimeAsync(CancellationToken ct)
	{
		return await GetRankingAsync(AnimeRankingType.Upcoming, ct);
	}

	public async Task<List<AnimeModel>> GetAiringTodayAsync(CancellationToken ct)
	{
		try
		{
			// Jikan's schedule is by Japanese broadcast day; the user's local day is the closest match we can ask for.
			var response = await s_jikan.GetScheduleAsync(ConvertDay(DateTime.Now.DayOfWeek), ct);
			return
			[
				.. response.Data
						   .Where(x => session.IncludeNsfw || x.Rating?.StartsWith("Rx", StringComparison.Ordinal) != true)
						   .DistinctBy(x => x.MalId)
						   .Select(MalToModelConverter.ConvertJikanModel)
			];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Jikan schedule request failed");
			return [];
		}
	}

	private async Task<List<AnimeModel>> GetRankingAsync(AnimeRankingType ranking, CancellationToken ct)
	{
		try
		{
			var client = await session.GetClientAsync();
			var request = client.Anime().Top(ranking)
								.WithFields(MalToModelConverter.ListFields)
								.WithLimit(session.SearchLimit);

			if (session.IncludeNsfw)
			{
				request.IncludeNsfw();
			}

			var result = await request.Find();
			ct.ThrowIfCancellationRequested();
			return [.. result.Data.Select(x => MalToModelConverter.ConvertModel(x.Anime))];
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "MyAnimeList ranking request failed");
			return [];
		}
	}

	private static async Task<List<Genre>> LoadGenresAsync()
	{
		if (s_genres is { } genres)
		{
			return genres;
		}

		var response = await s_jikan.GetAnimeGenresAsync();
		return s_genres = response.Data?.ToList() ?? [];
	}

	private static async Task<IEnumerable<long>> GetGenreIdsAsync(IEnumerable<string> names)
	{
		var genres = await LoadGenresAsync();
		return names.Select(name => genres.FirstOrDefault(g => g.Name == name)?.MalId)
					.OfType<long>();
	}

	private static ScheduledDay ConvertDay(DayOfWeek dayOfWeek)
	{
		return dayOfWeek switch
		{
			DayOfWeek.Monday => ScheduledDay.Monday,
			DayOfWeek.Tuesday => ScheduledDay.Tuesday,
			DayOfWeek.Wednesday => ScheduledDay.Wednesday,
			DayOfWeek.Thursday => ScheduledDay.Thursday,
			DayOfWeek.Friday => ScheduledDay.Friday,
			DayOfWeek.Saturday => ScheduledDay.Saturday,
			DayOfWeek.Sunday => ScheduledDay.Sunday,
			_ => ScheduledDay.Unknown
		};
	}
}
