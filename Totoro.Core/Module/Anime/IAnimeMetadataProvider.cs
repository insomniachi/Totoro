namespace Totoro.Core.Module.Anime;

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
}
