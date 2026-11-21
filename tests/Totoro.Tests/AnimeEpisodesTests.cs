using System.Text.Json;
using Flurl.Http.Testing;
using Totoro.Core.Modules.Anime;
using Xunit;

namespace Totoro.Tests;

/// <summary>ani.zip and Tenrai, faked with Flurl's HttpTest: nothing here reaches the network.</summary>
public class AnimeEpisodesTests
{
	private const string AniZip = "https://api.ani.zip/mappings*";

	// Fillers are cached by MyAnimeList id for the whole run, so each test has its own.
	private static AnimeModel Anime(long malId, string? service = nameof(AnimeId.Anilist)) =>
		new() { Id = 21, ServiceName = service, ExternalIds = new AnimeId { Anilist = 21, MyAnimeList = malId } };

	private static string Tenrai(long malId) => $"https://api.tenrai.org/v1/anime/{malId}/episodes*";

	private static string FillersPage(bool hasNext, params (int Number, bool Filler)[] episodes) => JsonSerializer.Serialize(new
	{
		data = episodes.Select(x => new { mal_id = x.Number, filler = x.Filler }),
		pagination = new { has_next_page = hasNext },
	});

	[Fact]
	public async Task GetEpisodesAsync_RebasesNumbering_FlagsSpecialsAndFillers_SkipsUnaired()
	{
		using var http = new HttpTest();
		http.ForCallsTo(AniZip).RespondWith("""
			{"episodes":{
				"1":{"episodeNumber":13,"airDateUtc":"2020-01-01T00:00:00Z","title":{"en":"Thirteen"}},
				"2":{"episodeNumber":14,"airDateUtc":"2020-01-08T00:00:00Z"},
				"3":{"episodeNumber":15,"airDateUtc":"2999-01-01T00:00:00Z"},
				"4":{"episodeNumber":16},
				"S1":{"episodeNumber":1,"airDateUtc":"2020-02-01T00:00:00Z"},
				"5":null
			}}
			""");
		http.ForCallsTo(Tenrai(990001)).RespondWith(FillersPage(false, (1, false), (2, true)));

		var episodes = await Anime(990001).GetEpisodesAsync();

		Assert.Equal([(1, false, false), (2, false, true), (1, true, false)], episodes.Select(x => (x.EpisodeNumber, x.IsSpecial, x.IsFiller)));
		http.ShouldHaveCalled("https://api.ani.zip/mappings").WithQueryParam("anilist_id", 21);
	}

	[Fact]
	public async Task GetEpisodesAsync_UnsupportedDatabase_ReturnsNothing()
	{
		using var http = new HttpTest();

		Assert.Empty(await Anime(990002, service: null).GetEpisodesAsync());
		http.ShouldNotHaveCalled(AniZip);
	}

	[Fact]
	public async Task GetEpisodesAsync_ServerError_ReturnsNothing()
	{
		using var http = new HttpTest();
		http.ForCallsTo(AniZip).RespondWith("oops", 500);

		Assert.Empty(await Anime(990003).GetEpisodesAsync());
	}

	[Theory]
	[InlineData("https://artworks.thetvdb.com/banners/v4/series/1/clearlogo/abc.png", true)]
	[InlineData("https://artworks.thetvdb.com/banners/v4/series/1/icon/abc.png", false)]
	public async Task GetLogoAsync_OnlyReturnsRealClearLogos(string url, bool isLogo)
	{
		using var http = new HttpTest();
		http.ForCallsTo(AniZip).RespondWith($$"""{"images":[{"coverType":"Banner","url":"https://x/banner.png"},{"coverType":"Clearlogo","url":"{{url}}"}]}""");

		Assert.Equal(isLogo ? url : null, await Anime(990004, nameof(AnimeId.MyAnimeList)).GetLogoAsync());
		http.ShouldHaveCalled("https://api.ani.zip/mappings").WithQueryParam("mal_id", 21);
	}

	[Fact]
	public async Task GetLogoAsync_UnsupportedDatabaseOrError_ReturnsNull()
	{
		using var http = new HttpTest();
		http.ForCallsTo(AniZip).RespondWith("", 500);

		Assert.Null(await Anime(990005, service: null).GetLogoAsync());
		Assert.Null(await Anime(990005).GetLogoAsync());
	}

	[Fact]
	public async Task GetFillersAsync_ReadsEveryPage_ThenCaches()
	{
		using var http = new HttpTest();
		http.ForCallsTo(Tenrai(990006))
			.RespondWith(FillersPage(true, (1, false), (2, true)))
			.RespondWith(FillersPage(false, (3, true), (4, false)));
		var anime = Anime(990006);

		Assert.Equal([2, 3], (await anime.GetFillersAsync()).Order());
		Assert.Equal([2, 3], (await anime.GetFillersAsync()).Order());
		http.ShouldHaveCalled(Tenrai(990006)).Times(2);
		http.ShouldHaveCalled(Tenrai(990006)).WithQueryParam("page", 2);
	}

	[Fact]
	public async Task GetFillersAsync_UsesOwnIdForMyAnimeListAnime()
	{
		using var http = new HttpTest();
		http.ForCallsTo("https://api.tenrai.org/v1/anime/21/episodes*").RespondWith(FillersPage(false, (5, true)));
		var anime = new AnimeModel { Id = 21, ServiceName = nameof(AnimeId.MyAnimeList) };

		Assert.Equal([5], await anime.GetFillersAsync());
	}

	[Fact]
	public async Task GetFillersAsync_NotOnMyAnimeList_ReturnsNothingWithoutAsking()
	{
		using var http = new HttpTest();

		Assert.Empty(await Anime(0).GetFillersAsync());
		http.ShouldNotHaveMadeACall();
	}

	[Fact]
	public async Task GetFillersAsync_Failure_IsntCached()
	{
		using var http = new HttpTest();
		http.ForCallsTo(Tenrai(990007))
			.RespondWith("", 500)
			.RespondWith(FillersPage(false, (7, true)));
		var anime = Anime(990007);

		Assert.Empty(await anime.GetFillersAsync());
		Assert.Equal([7], await anime.GetFillersAsync());
	}
}
