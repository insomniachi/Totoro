using Totoro.Core.Modules.Anime;
using Xunit;

namespace Totoro.Tests;

public class BrowserEpisodeTitleTests
{
	[Theory]
	[InlineData("Frieren Episode 5 English Sub - HiAnime", null, "Frieren", 5)]
	[InlineData("Watch Dandadan Ep 3 | Crunchyroll", null, "Dandadan", 3)]
	[InlineData("Watch One Piece - Ep. 1100", null, "One Piece", 1100)]
	[InlineData("Spy x Family S01E07", null, "Spy x Family", 7)]
	[InlineData("E5 - The Priest's Lie", "Frieren: Beyond Journey's End", "Frieren: Beyond Journey's End", 5)]
	public void Parse_Episode_IsTitleAndNumber(string title, string? artist, string anime, int episode)
	{
		Assert.Equal((anime, episode), BrowserEpisodeTitle.Parse(title, artist));
	}

	[Theory]
	[InlineData("Lofi hip hop radio - beats to relax/study to", "Lofi Girl")]
	[InlineData("Episode 4", null)]
	[InlineData("Frieren Episode 0", null)]
	[InlineData(null, "Frieren")]
	public void Parse_NotAnEpisode_IsNull(string? title, string? artist)
	{
		Assert.Null(BrowserEpisodeTitle.Parse(title, artist));
	}
}
