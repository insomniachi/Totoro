using System.Reactive.Linq;
using Totoro.Core.Modules.Anime;
using Xunit;

namespace Totoro.Tests;

public class UserListFilterTests
{
	private static AnimeModel Anime(string title, string english = "", string[]? genres = null, Season? season = null, int? score = null) => new()
	{
		Title = title,
		EngTitle = english,
		Genres = genres ?? [],
		Season = season,
		Tracking = new Tracking { Status = ListItemStatus.Watching, Score = score },
	};

	private static Func<AnimeModel, bool> Current(Totoro.Core.Filtering.IFilter<AnimeModel> filter) => filter.Predicate.FirstAsync().Wait();

	[Fact]
	public void Title_MatchesAnyTitle_IgnoringCaseAndAccents()
	{
		var anime = Anime("Sousou no Frieren", "Frieren: Beyond Journey's End");
		Assert.True(UserListQuery.MatchesTitle(anime, "frieren"));
		Assert.True(UserListQuery.MatchesTitle(anime, "BEYOND"));
		Assert.True(UserListQuery.MatchesTitle(Anime("Pokémon"), "pokemon"));
		Assert.False(UserListQuery.MatchesTitle(anime, "naruto"));
		Assert.True(UserListQuery.MatchesTitle(anime, "  "));
	}

	[Fact]
	public void Genres_NeedEveryTickedOne()
	{
		var both = Anime("A", genres: ["Romance", "Comedy"]);
		var one = Anime("B", genres: ["Romance"]);
		var filter = new GenreFilter();
		filter.SetGenres([both, one]);

		Assert.Equal(["Comedy", "Romance"], filter.Options.Select(x => x.Name));
		filter.Options.Single(x => x.Name == "Romance").IsSelected = true;
		filter.Options.Single(x => x.Name == "Comedy").IsSelected = true;

		Assert.True(Current(filter)(both));
		Assert.False(Current(filter)(one));
		Assert.Equal("Genres (2)", filter.Label);
	}

	[Fact]
	public void Genres_StayTicked_WhenTheListReloads()
	{
		var anime = Anime("A", genres: ["Romance"]);
		var filter = new GenreFilter();
		filter.SetGenres([anime]);
		filter.Options[0].IsSelected = true;

		filter.SetGenres([anime, Anime("B", genres: ["Action"])]);

		Assert.True(filter.Options.Single(x => x.Name == "Romance").IsSelected);
	}

	[Fact]
	public void Season_MatchesSeasonAndYear()
	{
		var fall2023 = Anime("A", season: new Season(AnimeSeason.Fall, 2023));
		var spring2023 = Anime("B", season: new Season(AnimeSeason.Spring, 2023));
		var filter = new SeasonFilter();
		filter.SetYears([fall2023, spring2023]);

		filter.Season = filter.Seasons.Single(x => x.Value == AnimeSeason.Fall);
		Assert.True(Current(filter)(fall2023));
		Assert.False(Current(filter)(spring2023));

		filter.Season = filter.Seasons[0];
		filter.Year = filter.Years.Single(x => x.Value == 2023);
		Assert.True(Current(filter)(spring2023));
		Assert.True(filter.IsActive);

		filter.Clear();
		Assert.False(filter.IsActive);
	}

	[Fact]
	public void Sort_ByScore_PutsUnscoredLast()
	{
		var high = Anime("High", score: 9);
		var low = Anime("Low", score: 5);
		var none = Anime("None");

		Assert.Equal(["High", "Low", "None"], UserListQuery.Sort([none, low, high], ListSort.UserScore, descending: true).Select(x => x.Title));
		Assert.Equal(["Low", "High", "None"], UserListQuery.Sort([none, low, high], ListSort.UserScore, descending: false).Select(x => x.Title));
	}

	[Fact]
	public void Sort_ByTitle_AToZ()
	{
		Assert.Equal(["Bocchi", "frieren", "Mushoku"], UserListQuery.Sort([Anime("Mushoku"), Anime("frieren"), Anime("Bocchi")], ListSort.Title, descending: false).Select(x => x.Title));
	}
}
