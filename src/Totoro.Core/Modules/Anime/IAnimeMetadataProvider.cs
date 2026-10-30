namespace Totoro.Core.Modules.Anime;

/// <summary>
/// A source of anime information (titles, artwork, episodes, characters, search...), e.g. AniList.
/// Modules register implementations keyed by their module id.
/// </summary>
public interface IAnimeMetadataProvider
{
	Guid Id { get; }
	string Name { get; }
	Task<AnimeModel> GetAnimeAsync(long id);
	Task<List<AnimeModel>> SearchAnimeAsync(string term);
	Task<List<AnimeModel>> SearchAnimeAsync(AdvancedSearchRequest request);
	Task<List<EpisodeInfo>> GetEpisodesAsync(AnimeModel anime);
	Task<List<CharacterModel>> GetCharactersAsync(long animeId);
	Task<List<string>> GetGenresAsync();
	Task<List<AnimeModel>> GetPopularAnimeAsync(CancellationToken ct);
	Task<List<AnimeModel>> GetUpcomingAnimeAsync(CancellationToken ct);
	Task<List<AnimeModel>> GetAiringTodayAsync(CancellationToken ct);

	/// <summary>
	/// The anime that started airing in <paramref name="season"/>, most popular first, each with its related anime
	/// (<see cref="AnimeModel.Related"/>, with how each relates), e.g. to find which are sequels.
	/// </summary>
	Task<List<AnimeModel>> GetSeasonAnimeAsync(Season season, CancellationToken ct);

	/// <summary>
	/// The anime with these ids, each with its related anime (<see cref="AnimeModel.Related"/>), e.g. to find sequels the
	/// user hasn't watched. Empty from a database that can't fetch them in bulk.
	/// </summary>
	Task<List<AnimeModel>> GetRelationsAsync(IReadOnlyCollection<long> ids, CancellationToken ct) => Task.FromResult(new List<AnimeModel>());
}
