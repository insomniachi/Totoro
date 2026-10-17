using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Relations;
using Totoro.Core.Settings;
using Totoro.Playback;
using Xunit;

namespace Totoro.Tests;

public class PickEpisodeTests
{
	private sealed class FakeAnimeRelations(params AnimeRelationRule[] rules) : IAnimeRelations
	{
		public event EventHandler? Changed { add { } remove { } }
		public AnimeRelationsInfo Info => new(rules.Length, null, null);
		public IReadOnlyList<AnimeRelationRule> UserRules => [];

		public AnimeRelationMatch? FindAbsoluteEpisode(AnimeModel anime, int episode)
		{
			var ids = RelationIds.Of(anime);
			foreach (var rule in rules)
			{
				if (rule.Destination.Matches(ids) && rule.ToSourceEpisode(episode) is { } absolute)
				{
					return new AnimeRelationMatch(rule, absolute, false);
				}
			}

			return null;
		}

		public IReadOnlyList<AnimeRelationMatch> GetRules(AnimeModel anime)
		{
			var ids = RelationIds.Of(anime);
			return [.. rules.Where(x => x.Destination.Matches(ids) || x.Source.Matches(ids))
							.Select(x => new AnimeRelationMatch(x, x.SourceEpisodes.First, false))];
		}

		public void AddUserRule(AnimeRelationRule rule) { }
		public void ReplaceUserRule(int index, AnimeRelationRule rule) { }
		public void RemoveUserRule(int index) { }
		public Task<bool> UpdateAsync(CancellationToken ct) => Task.FromResult(true);
		public void UpdateIfStale() { }
		public Task EnsureAvailableAsync(CancellationToken ct) => Task.CompletedTask;
	}

	[Fact]
	public void PickEpisode_WhenAbsoluteEpisodeAvailable_PicksMappedEpisode()
	{
		// Kusuriya no Hitorigoto (Apothecary Diaries) Season 3:
		// Season 1 has episodes 1-24, Season 2 has 25-48, Season 3 starts at 49.
		var rule = AnimeRelationRule.Parse("- 54492|47083|161645:49-60 -> 61987|50021|195516:1-12!")!;
		var relations = new FakeAnimeRelations(rule);
		var anime = new AnimeModel
		{
			Title = "The Apothecary Diaries Season 3",
			TotalEpisodes = 12,
			ExternalIds = new AnimeId { MyAnimeList = 61987, Anilist = 195516 },
		};

		// Provider has episodes 1 through 50 (seasons 1 & 2, and episodes 1 & 2 of season 3)
		var providerEpisodes = Enumerable.Range(1, 50)
			.Select(i => new ProviderEpisode("show1", $"ep_{i}", i))
			.ToList();

		// Season 3, Episode 1 -> mapped to absolute 49
		var ep1 = WatchService.PickEpisode(providerEpisodes, anime, 1, null, relations);
		Assert.NotNull(ep1);
		Assert.Equal(49, (int)ep1.Number);

		// Season 3, Episode 2 -> mapped to absolute 50
		var ep2 = WatchService.PickEpisode(providerEpisodes, anime, 2, null, relations);
		Assert.NotNull(ep2);
		Assert.Equal(50, (int)ep2.Number);
	}

	[Fact]
	public void PickEpisode_WhenNextAbsoluteEpisodeMissing_ReturnsNullInsteadOfSeason1Episode()
	{
		// Season 3 maps 1-12 to 49-60.
		var rule = AnimeRelationRule.Parse("- 54492|47083|161645:49-60 -> 61987|50021|195516:1-12!")!;
		var relations = new FakeAnimeRelations(rule);
		var anime = new AnimeModel
		{
			Title = "The Apothecary Diaries Season 3",
			TotalEpisodes = 12,
			ExternalIds = new AnimeId { MyAnimeList = 61987, Anilist = 195516 },
		};

		// Provider only has episodes 1 through 50 (so episode 51 is not yet released/available)
		var providerEpisodes = Enumerable.Range(1, 50)
			.Select(i => new ProviderEpisode("show1", $"ep_{i}", i))
			.ToList();

		// Season 3, Episode 3 -> mapped to absolute 51, which is missing from provider.
		// It MUST NOT pick episode 3 from season 1! It should return null (fail cleanly).
		var ep3 = WatchService.PickEpisode(providerEpisodes, anime, 3, null, relations);
		Assert.Null(ep3);
	}

	[Fact]
	public void PickEpisode_WhenSeasonNumberingExplicitlySet_PicksSeasonEpisode()
	{
		var rule = AnimeRelationRule.Parse("- 54492|47083|161645:49-60 -> 61987|50021|195516:1-12!")!;
		var relations = new FakeAnimeRelations(rule);
		var anime = new AnimeModel
		{
			Title = "The Apothecary Diaries Season 3",
			TotalEpisodes = 12,
			ExternalIds = new AnimeId { MyAnimeList = 61987, Anilist = 195516 },
		};

		var providerEpisodes = Enumerable.Range(1, 50)
			.Select(i => new ProviderEpisode("show1", $"ep_{i}", i))
			.ToList();

		// When user explicitly specifies Season numbering
		var ep3 = WatchService.PickEpisode(providerEpisodes, anime, 3, EpisodeNumbering.Season, relations);
		Assert.NotNull(ep3);
		Assert.Equal(3, (int)ep3.Number);
	}

	[Fact]
	public void PickEpisode_WhenAbsoluteSetOnFirstSeason_PicksSameNumber()
	{
		// A first season isn't the destination of any rule: its absolute numbers are its own.
		var relations = new FakeAnimeRelations();
		var anime = new AnimeModel
		{
			Title = "The Apothecary Diaries",
			TotalEpisodes = 24,
			ExternalIds = new AnimeId { MyAnimeList = 54492, Anilist = 161645 },
		};

		var providerEpisodes = Enumerable.Range(1, 24)
			.Select(i => new ProviderEpisode("show1", $"ep_{i}", i))
			.ToList();

		var ep3 = WatchService.PickEpisode(providerEpisodes, anime, 3, EpisodeNumbering.Absolute, relations);
		Assert.NotNull(ep3);
		Assert.Equal(3, (int)ep3.Number);
	}

	[Fact]
	public void PickEpisode_WhenAbsoluteSetOnLaterSeasonWithoutMapping_ReturnsNull()
	{
		// Season 3 maps 1-12 to 49-60, so episode 13 has no mapping; it must not be season 1's episode 13.
		var rule = AnimeRelationRule.Parse("- 54492|47083|161645:49-60 -> 61987|50021|195516:1-12!")!;
		var relations = new FakeAnimeRelations(rule);
		var anime = new AnimeModel
		{
			Title = "The Apothecary Diaries Season 3",
			ExternalIds = new AnimeId { MyAnimeList = 61987, Anilist = 195516 },
		};

		var providerEpisodes = Enumerable.Range(1, 30)
			.Select(i => new ProviderEpisode("show1", $"ep_{i}", i))
			.ToList();

		Assert.Null(WatchService.PickEpisode(providerEpisodes, anime, 13, EpisodeNumbering.Absolute, relations));
	}

	[Fact]
	public void PickEpisode_WhenProviderUsesSeasonNumbering_PicksSeasonEpisode()
	{
		var rule = AnimeRelationRule.Parse("- 54492|47083|161645:49-60 -> 61987|50021|195516:1-12!")!;
		var relations = new FakeAnimeRelations(rule);
		var anime = new AnimeModel
		{
			Title = "The Apothecary Diaries Season 3",
			TotalEpisodes = 12,
			ExternalIds = new AnimeId { MyAnimeList = 61987, Anilist = 195516 },
		};

		// Provider only has Season 3 (episodes 1 to 12)
		var providerEpisodes = Enumerable.Range(1, 12)
			.Select(i => new ProviderEpisode("show1", $"ep_{i}", i))
			.ToList();

		var ep1 = WatchService.PickEpisode(providerEpisodes, anime, 1, null, relations);
		Assert.NotNull(ep1);
		Assert.Equal(1, (int)ep1.Number);

		var ep3 = WatchService.PickEpisode(providerEpisodes, anime, 3, null, relations);
		Assert.NotNull(ep3);
		Assert.Equal(3, (int)ep3.Number);
	}
}
