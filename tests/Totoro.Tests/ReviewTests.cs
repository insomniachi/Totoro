using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.Modules.AnimeDatabase.MyAnimeList;
using Xunit;

namespace Totoro.Tests;

public class ReviewTests
{
	private static readonly ReviewModel s_anilistReview = new("Anilist", "a", null, "From AniList", "", 80, null, null, null);
	private static readonly ReviewModel s_ownReview = new("Kitsu", "k", null, "Its own", "", null, "3 upvotes", null, null);

	// An anime from Kitsu whose AniList id is known, so the fallback needs no lookup.
	private static AnimeModel Anime() => new() { Id = 1, ServiceName = "Kitsu", ExternalIds = new AnimeId { Kitsu = 1, Anilist = 99 } };

	[Fact]
	public async Task OwnReviewsWhenItHasThem()
	{
		var anilist = new FakeDatabase("Anilist", _ => [s_anilistReview]);
		var kitsu = new FakeDatabase("Kitsu", _ => [s_ownReview]);
		var reviews = await Service(anilist, kitsu).GetReviewsAsync(kitsu, Anime());
		Assert.Equal([s_ownReview], reviews);
		Assert.Empty(anilist.Asked);
	}

	[Fact]
	public async Task AnilistsByItsAnilistIdWhenItHasNone()
	{
		var anilist = new FakeDatabase("Anilist", _ => [s_anilistReview]);
		var kitsu = new FakeDatabase("Kitsu", _ => []);
		Assert.Equal([s_anilistReview], await Service(anilist, kitsu).GetReviewsAsync(kitsu, Anime()));
		Assert.Equal([99L], anilist.Asked);
	}

	[Fact]
	public async Task AnilistsWhenItsOwnFail()
	{
		var anilist = new FakeDatabase("Anilist", _ => [s_anilistReview]);
		var kitsu = new FakeDatabase("Kitsu", _ => throw new HttpRequestException("rate limited"));
		Assert.Equal([s_anilistReview], await Service(anilist, kitsu).GetReviewsAsync(kitsu, Anime()));
	}

	[Fact]
	public async Task AnilistsOwnFailuresAreNotHidden()
	{
		var anilist = new FakeDatabase("Anilist", _ => throw new HttpRequestException("down"));
		await Assert.ThrowsAsync<HttpRequestException>(() => Service(anilist).GetReviewsAsync(anilist, Anime()));
	}

	[Fact]
	public async Task OwnFailureShowsWhenAnilistCantStandIn()
	{
		// No AniList installed, so nothing can stand in: the failure isn't passed off as "no reviews".
		var kitsu = new FakeDatabase("Kitsu", _ => throw new HttpRequestException("rate limited"));
		await Assert.ThrowsAsync<HttpRequestException>(() => Service(kitsu).GetReviewsAsync(kitsu, Anime()));
	}

	[Fact]
	public void MalReviewFromTenrai()
	{
		// The reactions count is formatted in the user's culture; pinned here, and put back after.
		var culture = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		ReviewModel review;
		try
		{
			review = MyAnimeListMetadataProvider.ConvertReview(JsonNode.Parse("""
				{ "url": "https://myanimelist.net/reviews.php?id=1", "date": "2024-03-24T09:03:00+00:00", "score": 7,
				  "tags": ["Mixed Feelings"], "reactions": { "overall": 5953 },
				  "review": "First <line>\nstill first\n\nSecond & last",
				  "user": { "username": "u", "images": { "jpg": { "image_url": "https://cdn/u.jpg" } } } }
				""")!);
		}
		finally
		{
			CultureInfo.CurrentCulture = culture;
		}

		Assert.Equal("MyAnimeList", review.Source);
		Assert.Equal("u", review.Author);
		Assert.Equal("https://cdn/u.jpg", review.AuthorImage);
		Assert.Equal("First <line> still first Second & last", review.Summary);
		Assert.Equal("<p>First &lt;line&gt;<br>still first</p><p>Second &amp; last</p>", review.Body);
		Assert.Equal(70, review.Score);
		Assert.Equal("Mixed Feelings · 5,953 reactions", review.Reception);
		Assert.Equal(new DateTimeOffset(2024, 3, 24, 9, 3, 0, TimeSpan.Zero), review.Created);
	}

	[Fact]
	public void LongFirstLineIsCutAtAWord()
	{
		var excerpt = MyAnimeListMetadataProvider.Excerpt(string.Join(' ', Enumerable.Repeat("word", 60)));
		Assert.True(excerpt.Length <= 161);
		Assert.EndsWith("word…", excerpt);
	}

	private static AnimeReviewService Service(params IAnimeMetadataProvider[] databases) =>
		new(new Databases(databases), NullLogger<AnimeReviewService>.Instance);

	private sealed class Databases(IAnimeMetadataProvider[] items) : IFactory<IAnimeMetadataProvider>
	{
		public IAnimeMetadataProvider? Create(Guid id) => null;
		public IAnimeMetadataProvider? CreateDefault() => items.FirstOrDefault();
		public IEnumerable<IAnimeMetadataProvider> CreateAll() => items;
	}

	private sealed class FakeDatabase(string name, Func<long, List<ReviewModel>> reviews) : IAnimeMetadataProvider
	{
		public List<long> Asked { get; } = [];
		public Guid Id => Guid.Empty;
		public string Name => name;

		public Task<List<ReviewModel>> GetReviewsAsync(long animeId)
		{
			Asked.Add(animeId);
			return Task.FromResult(reviews(animeId));
		}

		public Task<AnimeModel> GetAnimeAsync(long id) => throw new NotSupportedException();
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
