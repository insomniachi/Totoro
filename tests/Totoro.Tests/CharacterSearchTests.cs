using Totoro.Core.Services;
using Xunit;

namespace Totoro.Tests;

public class CharacterSearchTests
{
	[Fact]
	public void Parse_ReadsEachCharacterLeftToRight_WithItsBestCandidate()
	{
		const string reply = """
			{ "code": 0, "ai": false, "data": [
			  { "box": [0.6, 0.1, 0.9, 0.5], "not_confident": false, "character": [ { "work": "Frieren", "character": "Fern" } ] },
			  { "box": [0.1, 0.1, 0.4, 0.5], "not_confident": true,
			    "character": [ { "work": "Bocchi the Rock!", "character": "Hitori Gotoh" }, { "work": "Other", "character": "Someone" } ] },
			  { "box": [0.4, 0.1, 0.5, 0.5], "character": [] }
			] }
			""";

		var found = CharacterSearchService.Parse(reply);

		Assert.Equal(
			[new CharacterMatch("Hitori Gotoh", "Bocchi the Rock!", true), new CharacterMatch("Fern", "Frieren", false)],
			found);
	}

	[Theory]
	[InlineData("ジャン・キルシュタイン", "Jean Kirstein")] // Same Japanese name.
	[InlineData("ジャン キルシュタイン", "Jean Kirstein")]  // A space for the dot.
	[InlineData("ヒンメル", "Himmel")]
	[InlineData("ミカサ", "Mikasa Ackerman")]               // Given name alone.
	[InlineData("寺門通", null)]                            // Not in the cast.
	public void EnglishName_FindsTheCastMember(string native, string? expected)
	{
		Totoro.Core.Modules.Anime.CharacterModel[] cast =
		[
			new() { Name = "Eren Yeager", NativeName = "エレン・イェーガー" },
			new() { Name = "Mikasa Ackerman", NativeName = "ミカサ・アッカーマン" },
			new() { Name = "Jean Kirstein", NativeName = "ジャン・キルシュタイン" },
			new() { Name = "Himmel", NativeName = "ヒンメル" },
			new() { Name = "No Japanese name" },
		];

		Assert.Equal(expected, CharacterSearchService.EnglishName(native, cast));
	}

	[Theory]
	[InlineData(17728, "usage limit")]
	[InlineData(17708, "too many people")]
	[InlineData(99999, "99999")]
	public void Parse_ThrowsWhatAnimeTraceReported(int code, string expected)
	{
		var ex = Assert.Throws<InvalidOperationException>(() => CharacterSearchService.Parse($$"""{ "code": {{code}}, "data": [] }"""));
		Assert.Contains(expected, ex.Message);
	}
}
