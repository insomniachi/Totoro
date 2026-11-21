using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Helpers;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;
using Xunit;

namespace Totoro.Tests;

/// <summary>
/// The rules kept on this PC (in the tests' own folder, see <see cref="TestFiles"/>). Never updates from GitHub: the
/// downloaded copy is always there and fresh, so nothing here goes to the network.
/// </summary>
public sealed class AnimeRelationsTests : IDisposable
{
	private const string Upstream = """
		- version: 1.3.0
		- last_modified: 2026-10-01

		::rules

		# Seihantai na Kimi to Boku -> ~ 2nd Season
		- 1|?|10:13-25 -> 2|?|20:1-13!

		# Open-ended, same MyAnimeList id
		- 5|?|50:1-? -> ~|?|51:13-?

		- not a rule
		""";

	private static readonly string s_downloaded = FileHelper.GetPath("anime-relations.txt");
	private static readonly string s_user = FileHelper.GetPath("anime-relations.user.txt");

	public AnimeRelationsTests()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(s_downloaded)!);
		Dispose();
		File.WriteAllText(s_downloaded, Upstream);
	}

	public void Dispose()
	{
		File.Delete(s_downloaded);
		File.Delete(s_user);
	}

	private static AnimeRelations Create() => new(NullLogger<AnimeRelations>.Instance);

	private static AnimeModel Anime(long mal = 0, long anilist = 0) => new() { ExternalIds = new AnimeId { MyAnimeList = mal, Anilist = anilist } };

	[Fact]
	public void Loads_TheDownloadedRules()
	{
		var info = Create().Info;

		Assert.Equal(2, info.RuleCount);
		Assert.Equal("2026-10-01", info.LastModified);
		Assert.NotNull(info.LastChecked);
	}

	[Theory]
	[InlineData(1, 13)]
	[InlineData(4, 16)]
	[InlineData(13, 25)]
	public void FindAbsoluteEpisode_MapsTheSeasonsEpisodes(int episode, int absolute)
	{
		var match = Create().FindAbsoluteEpisode(Anime(mal: 2), episode);

		Assert.Equal(absolute, match?.Episode);
		Assert.False(match?.IsUserRule);
		Assert.Equal("Seihantai na Kimi to Boku -> ~ 2nd Season", match?.Rule.Title);
	}

	[Fact]
	public void FindAbsoluteEpisode_OutsideTheRule_OrUnknownAnime_IsNull()
	{
		var relations = Create();

		Assert.Null(relations.FindAbsoluteEpisode(Anime(mal: 2), 14));
		Assert.Null(relations.FindAbsoluteEpisode(Anime(mal: 999), 1));
		// The source season isn't the destination.
		Assert.Null(relations.FindAbsoluteEpisode(Anime(mal: 1), 1));
	}

	[Fact]
	public void FindAbsoluteEpisode_OpenEndedRule_ByAniListId()
	{
		// "~" repeats the source's MyAnimeList id; matched here by the destination's AniList id alone.
		var match = Create().FindAbsoluteEpisode(Anime(anilist: 51), 100);

		Assert.Equal(88, match?.Episode);
		Assert.Equal(new RelationIds(5, null, 51), match?.Rule.Destination);
	}

	[Fact]
	public void GetRules_FindsBothSides()
	{
		var relations = Create();

		Assert.Single(relations.GetRules(Anime(mal: 1)));
		Assert.Equal(13, relations.GetRules(Anime(mal: 2)).Single().Episode);
		Assert.Empty(relations.GetRules(Anime(mal: 999)));
	}

	[Fact]
	public void UserRules_ComeFirst_AreSaved_AndCanBeChanged()
	{
		var relations = Create();
		var changed = 0;
		relations.Changed += (_, _) => changed++;
		var rule = AnimeRelationRule.Parse("- 1|?|10:25-37 -> 2|?|20:1-13", "Mine")!;

		relations.AddUserRule(rule);

		var match = relations.FindAbsoluteEpisode(Anime(mal: 2), 1);
		Assert.True(match?.IsUserRule);
		Assert.Equal(25, match?.Episode);

		// Kept in its own file, title and all.
		var reloaded = Create();
		Assert.Equal([rule], reloaded.UserRules);
		Assert.Equal("Mine", reloaded.UserRules[0].Title);

		var replacement = AnimeRelationRule.Parse("- 1|?|10:30-? -> 2|?|20:1-?")!;
		reloaded.ReplaceUserRule(0, replacement);
		Assert.Equal([replacement], Create().UserRules);

		reloaded.RemoveUserRule(0);
		Assert.Empty(Create().UserRules);
		Assert.Equal(13, Create().FindAbsoluteEpisode(Anime(mal: 2), 1)?.Episode);

		Assert.Equal(1, changed);
	}

	[Theory]
	[InlineData("- 1|?|10:13-25 -> 2|?|20:1-13!")]
	[InlineData("- 5|?|50:1-? -> 5|?|51:13-?")]
	[InlineData("- 7|8|9:3 -> 7|8|9:3")]
	public void Rule_RoundTripsThroughItsLine(string line)
	{
		Assert.Equal(line, AnimeRelationRule.Parse(line)!.ToLine());
	}

	[Theory]
	[InlineData("")]
	[InlineData("- 1|2:1-2 -> 3|4|5:1-2")]
	[InlineData("- 1|2|3:x -> 4|5|6:1")]
	[InlineData("- 1|2|3:1-x -> 4|5|6:1")]
	public void Rule_ParseRejectsMalformedLines(string line)
	{
		Assert.Null(AnimeRelationRule.Parse(line));
	}
}
