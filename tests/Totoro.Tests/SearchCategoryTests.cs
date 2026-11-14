using Totoro.Core.Services;
using Xunit;

namespace Totoro.Tests;

public class SearchCategoryTests
{
	[Theory]
	[InlineData("a:frieren", SearchCategory.Anime, "frieren")]
	[InlineData("m: berserk", SearchCategory.Manga, "berserk")]
	[InlineData("c:Ai Hoshino", SearchCategory.Character, "Ai Hoshino")]
	[InlineData("  V: kana ", SearchCategory.VoiceActor, "kana")]
	[InlineData("s:", SearchCategory.Song, "")]
	public void Prefix_picks_the_category_and_goes(string text, SearchCategory category, string query)
	{
		Assert.Equal(((SearchCategory?)category, query), SearchCategories.Parse(text));
	}

	[Theory]
	[InlineData("frieren")]
	[InlineData("Re:Zero")]
	[InlineData("x: nothing")]
	public void Without_a_prefix_the_text_is_searched_as_it_is(string text)
	{
		Assert.Equal(((SearchCategory?)null, text), SearchCategories.Parse(text));
	}

	// Made-up, in AnimeThemes' GraphQL search layout.
	private const string SongSearch = """
		{"data":{"search":{"animethemes":[
		  {"type":"OP","sequence":null,
		   "anime":{"title":{"romaji":"Some Show"},
		     "resources":{"nodes":[{"site":"MAL","externalId":52034},{"site":"ANILIST","externalId":150672},{"site":"X","externalId":null}]},
		     "images":{"nodes":[{"facet":"SMALL_COVER","link":"https://img/small.avif"},{"facet":"LARGE_COVER","link":"https://img/large.png"}]}},
		   "song":{"title":{"romaji":"Idol"},"performances":[{"artist":{"name":{"main":"Singer"}}}]},
		   "animethemeentries":[{"episodes":"1-11","videos":{"nodes":[{"link":"https://v/op.webm","audio":{"link":"https://a/op.ogg"}}]}}]},
		  {"type":"ED","sequence":2,"anime":{"title":{"romaji":"No Video Yet"}},"song":{"title":{"romaji":"Soon"},"performances":[]},
		   "animethemeentries":[{"episodes":null,"videos":{"nodes":[]}}]},
		  {"type":"PV","sequence":1,"anime":{"title":{"romaji":"Skipped"}},"song":{"title":{"romaji":"Not a song"}}}
		]}}}
		""";

	[Fact]
	public void Song_search_gives_each_song_with_its_anime()
	{
		var matches = AnimeSongService.ParseSongMatches(SongSearch);

		Assert.Equal(2, matches.Count);
		// A song with no video yet is listed, with nothing to play.
		Assert.Equal(new AnimeSong(AnimeSongKind.Ending, 2, "Soon", "", "", null, null), matches[1].Song);
		var match = matches[0];
		Assert.Equal(new AnimeSong(AnimeSongKind.Opening, null, "Idol", "Singer", "1–11", new Uri("https://v/op.webm"), new Uri("https://a/op.ogg")), match.Song);
		Assert.Equal("Some Show", match.Anime.Title);
		Assert.Equal("https://img/large.png", match.Anime.Image);
		Assert.Equal(52034, match.Anime.ExternalIds.MyAnimeList);
		Assert.Equal(150672, match.Anime.ExternalIds.Anilist);
	}
}
