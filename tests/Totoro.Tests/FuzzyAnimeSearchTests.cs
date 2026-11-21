using Totoro.Core.Modules.Anime;
using Xunit;

namespace Totoro.Tests;

public class FuzzyAnimeSearchTests
{
	private static readonly AnimeModel s_jujutsu = new() { Id = 1, Title = "Jujutsu Kaisen", Popularity = 100 };
	private static readonly AnimeModel s_jujutsuMovie = new() { Id = 2, Title = "Jujutsu Kaisen 0", Popularity = 50 };
	private static readonly AnimeModel s_frieren = new() { Id = 3, Title = "Sousou no Frieren", EngTitle = "Frieren", Popularity = 80 };
	private static readonly AnimeModel s_onePiece = new() { Id = 4, Title = "One Piece", Popularity = 200 };
	private static readonly AnimeModel s_oneOuts = new() { Id = 5, Title = "One Outs", Popularity = 150 };

	[Fact]
	public async Task FindAnimeAsync_ExactMatch_ReturnsDatabaseResultsWithoutRetrying()
	{
		var database = new FakeDatabase(s_jujutsu, s_onePiece);

		var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = "jujutsu kaisen" });

		Assert.Equal([1], results.Select(x => x.Id));
		Assert.Single(database.Searches);
	}

	[Fact]
	public async Task FindAnimeAsync_TypoInOneWord_FindsItByTheOtherWord()
	{
		var database = new FakeDatabase(s_jujutsu, s_jujutsuMovie, s_onePiece);

		var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = "Jujutsu Kaisn" });

		// The closer title first; the movie is close enough to be a result too.
		Assert.Equal([1, 2], results.Select(x => x.Id));
		// The first search, then the longest word alone, which finds a good enough match to stop.
		Assert.Equal(["Jujutsu Kaisn", "jujutsu"], database.Searches);
	}

	[Fact]
	public async Task FindAnimeAsync_DropsResultsThatArentClose()
	{
		// "one" finds both, but only One Piece is like "one peice".
		var database = new FakeDatabase(s_onePiece, s_oneOuts);

		var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = "one peice" });

		Assert.Equal([4], results.Select(x => x.Id));
	}

	[Fact]
	public async Task FindAnimeAsync_IgnoresApostrophesAndPunctuation()
	{
		var journey = new AnimeModel { Id = 6, Title = "Journey's End: The Movie" };
		var database = new FakeDatabase(journey);

		var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = "journeys endd" });

		Assert.Equal([6], results.Select(x => x.Id));
	}

	[Fact]
	public async Task FindAnimeAsync_TriesAtMostThreeMoreSearches()
	{
		var database = new FakeDatabase();

		var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = "aaaa bbbbb cccccc ddddddd eeeeeeee" });

		Assert.Empty(results);
		Assert.Equal(4, database.Searches.Count);
	}

	[Fact]
	public async Task FindAnimeAsync_OneMisspeltWord_FindsItOnTheUsersList()
	{
		var database = new FakeDatabase(s_frieren);

		var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = "frieran" }, new FakeTracker(s_onePiece, s_frieren));

		Assert.Equal([3], results.Select(x => x.Id));
	}

	[Fact]
	public async Task FindAnimeAsync_WithOtherFilters_DoesntSearchTheUsersList()
	{
		// The list can't be filtered by year, so it isn't used.
		var database = new FakeDatabase(s_frieren);

		var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = "frieran", MinYear = 2023 }, new FakeTracker(s_frieren));

		Assert.Empty(results);
	}

	[Fact]
	public async Task FindAnimeAsync_NoTitle_ReturnsDatabaseResults()
	{
		var database = new FakeDatabase(s_onePiece);

		var results = await database.FindAnimeAsync(new AdvancedSearchRequest { Title = " ?! " }, new FakeTracker(s_onePiece));

		Assert.Empty(results);
		Assert.Single(database.Searches);
	}

	[Fact]
	public async Task FindAnimeAsync_Cancelled_StopsBeforeRetrying()
	{
		var database = new FakeDatabase(s_jujutsu);

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			database.FindAnimeAsync(new AdvancedSearchRequest { Title = "jujutsu kaisn" }, ct: new CancellationToken(canceled: true)));
	}

	/// <summary>Finds anime with every word of the title as a whole word in one of their titles, like the real database.</summary>
	private sealed class FakeDatabase(params AnimeModel[] anime) : IAnimeMetadataProvider
	{
		public List<string> Searches { get; } = [];

		public Guid Id => Guid.Empty;
		public string Name => "Fake";

		public Task<List<AnimeModel>> SearchAnimeAsync(AdvancedSearchRequest request)
		{
			Searches.Add(request.Title ?? "");
			var words = Words(request.Title ?? "");
			return Task.FromResult<List<AnimeModel>>(
				words.Length == 0 ? [] : [.. anime.Where(a => new[] { a.Title, a.EngTitle }.Any(t => words.All(Words(t).Contains)))]);
		}

		private static string[] Words(string text) => text.ToLowerInvariant().Replace("'", "").Split([' ', ':'], StringSplitOptions.RemoveEmptyEntries);

		public Task<AnimeModel> GetAnimeAsync(long id) => throw new NotSupportedException();
		public Task<List<AnimeModel>> SearchAnimeAsync(string term) => throw new NotSupportedException();
		public Task<List<EpisodeInfo>> GetEpisodesAsync(AnimeModel anime) => throw new NotSupportedException();
		public Task<List<CharacterModel>> GetCharactersAsync(long animeId) => throw new NotSupportedException();
		public Task<List<string>> GetGenresAsync() => throw new NotSupportedException();
		public Task<List<AnimeModel>> GetPopularAnimeAsync(CancellationToken ct) => throw new NotSupportedException();
		public Task<List<AnimeModel>> GetUpcomingAnimeAsync(CancellationToken ct) => throw new NotSupportedException();
		public Task<List<AnimeModel>> GetAiringTodayAsync(CancellationToken ct) => throw new NotSupportedException();
		public Task<List<AnimeModel>> GetSeasonAnimeAsync(Season season, CancellationToken ct) => throw new NotSupportedException();
	}

	private sealed class FakeTracker(params AnimeModel[] list) : IAnimeTracker
	{
		public Guid Id => Guid.Empty;
		public string Name => "Fake";
		public Task<List<AnimeModel>> GetUserListAsync(CancellationToken ct) => Task.FromResult(list.ToList());
		public Task<Tracking> UpdateAsync(long id, Tracking tracking) => throw new NotSupportedException();
		public Task<bool> RemoveAsync(long id) => throw new NotSupportedException();
	}
}
