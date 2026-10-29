using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Modules.Manga;

/// <summary>
/// A source of manga information (titles, covers, search...), e.g. AniList. A database that has manga registers this
/// and <see cref="IMangaTracker"/> keyed by its module id, beside (or instead of) its anime ones.
/// </summary>
public interface IMangaMetadataProvider
{
	Guid Id { get; }
	string Name { get; }
	Task<MangaModel> GetMangaAsync(long id);
	Task<List<MangaModel>> SearchMangaAsync(string term, CancellationToken ct);
	Task<List<MangaModel>> GetPopularMangaAsync(CancellationToken ct);
	Task<List<MangaModel>> GetTrendingMangaAsync(CancellationToken ct);

	/// <summary>The genres manga can be searched by (see <see cref="SearchMangaAsync(AdvancedSearchRequest, CancellationToken)"/>); none if it can't search by genre.</summary>
	Task<List<string>> GetGenresAsync() => Task.FromResult<List<string>>([]);

	/// <summary>
	/// Manga matching the request's title, genres, score and years (when they started); its season, which manga haven't,
	/// is ignored. By default a search of the title alone, for databases that can't filter.
	/// </summary>
	Task<List<MangaModel>> SearchMangaAsync(AdvancedSearchRequest request, CancellationToken ct) =>
		string.IsNullOrWhiteSpace(request.Title) ? Task.FromResult<List<MangaModel>>([]) : SearchMangaAsync(request.Title, ct);
}
