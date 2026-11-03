using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;
using Xunit;

namespace Totoro.Tests;

public class SeasonMapTests
{
	private static AnimeModel Link(long id, AnimeRelationType relation) => new() { Id = id, Relation = relation };

	[Fact]
	public async Task GetSeriesAsync_WalksEveryBranch_InAiringOrder()
	{
		// S1 -> (movie, S2) -> S3; the movie aired between S2 and S3, the unannounced S4 has no season yet.
		var database = new FakeDatabase(
			new AnimeModel { Id = 1, Season = new(AnimeSeason.Spring, 2019), Related = [Link(2, AnimeRelationType.Sequel), Link(3, AnimeRelationType.Sequel), Link(9, AnimeRelationType.SideStory)] },
			new AnimeModel { Id = 2, Season = new(AnimeSeason.Winter, 2021), Related = [Link(1, AnimeRelationType.Prequel)] },
			new AnimeModel { Id = 3, Season = new(AnimeSeason.Fall, 2020), Related = [Link(1, AnimeRelationType.Prequel), Link(4, AnimeRelationType.Sequel)] },
			new AnimeModel { Id = 4, Season = new(AnimeSeason.Spring, 2021), Related = [Link(3, AnimeRelationType.Prequel), Link(5, AnimeRelationType.Sequel)] },
			new AnimeModel { Id = 5, Related = [Link(4, AnimeRelationType.Prequel)] });

		var series = await SeasonMap.GetSeriesAsync(database, await database.GetAnimeAsync(4), CancellationToken.None);

		Assert.Equal([1, 3, 2, 4, 5], series.Select(x => x.Id));
	}

	private sealed class FakeDatabase(params AnimeModel[] anime) : IAnimeMetadataProvider
	{
		public Guid Id => Guid.Empty;
		public string Name => "Fake";
		public Task<AnimeModel> GetAnimeAsync(long id) => Task.FromResult(anime.Single(x => x.Id == id));
		public Task<List<AnimeModel>> SearchAnimeAsync(string term) => throw new NotSupportedException();
		public Task<List<AnimeModel>> SearchAnimeAsync(AdvancedSearchRequest request) => throw new NotSupportedException();
		public Task<List<EpisodeInfo>> GetEpisodesAsync(AnimeModel anime) => throw new NotSupportedException();
		public Task<List<CharacterModel>> GetCharactersAsync(long animeId) => throw new NotSupportedException();
		public Task<List<string>> GetGenresAsync() => throw new NotSupportedException();
		public Task<List<AnimeModel>> GetPopularAnimeAsync(CancellationToken ct) => throw new NotSupportedException();
		public Task<List<AnimeModel>> GetUpcomingAnimeAsync(CancellationToken ct) => throw new NotSupportedException();
		public Task<List<AnimeModel>> GetAiringTodayAsync(CancellationToken ct) => throw new NotSupportedException();
		public Task<List<AnimeModel>> GetSeasonAnimeAsync(Season season, CancellationToken ct) => throw new NotSupportedException();
	}
}
