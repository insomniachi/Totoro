using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;
using Xunit;

namespace Totoro.Tests;

public class MissedRelationsTests
{
	private static AnimeModel Related(long id, AnimeRelationType relation, int popularity = 0, AiringStatus status = AiringStatus.FinishedAiring) => new()
	{
		Id = id,
		Relation = relation,
		Popularity = popularity,
		AiringStatus = status,
	};

	[Fact]
	public void Find_KeepsMissedSequelsAndSpinOffs_OncePopularFirst()
	{
		var watched = new AnimeModel
		{
			Id = 1,
			Tracking = new Tracking { Status = ListItemStatus.Completed },
			Related =
			[
				Related(2, AnimeRelationType.Sequel, popularity: 10),
				Related(3, AnimeRelationType.SpinOff, popularity: 50),
				Related(4, AnimeRelationType.Sequel), // on the list already
				Related(5, AnimeRelationType.Sequel, status: AiringStatus.NotYetAired),
				Related(6, AnimeRelationType.Summary),
				Related(7, AnimeRelationType.Alternative),
			],
		};
		var other = new AnimeModel { Id = 8, Related = [Related(2, AnimeRelationType.Prequel, popularity: 10)] };
		var planned = new AnimeModel { Id = 4, Tracking = new Tracking { Status = ListItemStatus.PlanToWatch } };

		var missed = MissedRelations.Find([watched, planned], [watched, other]);

		Assert.Equal([3, 2], missed.Select(x => x.Id));
	}

	[Fact]
	public void Group_JoinsAFranchiseThroughWatchedAndMissed()
	{
		static AnimeModel Watched(long id, params AnimeModel[] related) =>
			new() { Id = id, Tracking = new Tracking { Status = ListItemStatus.Completed }, Related = related };

		// 1 → 2 (watched) → 3 (missed); 4 ← 5 → 4 shared by two watched; 6 → 7 alone.
		var list = new[] { Watched(1), Watched(2), Watched(5), Watched(6), Watched(8) };
		var withRelations = new[]
		{
			Watched(1, Related(2, AnimeRelationType.Sequel)),
			Watched(2, Related(1, AnimeRelationType.Prequel), Related(3, AnimeRelationType.Sequel, popularity: 5)),
			Watched(5, Related(4, AnimeRelationType.SideStory, popularity: 9)),
			Watched(8, Related(4, AnimeRelationType.SideStory, popularity: 9)),
			Watched(6, Related(7, AnimeRelationType.SpinOff, popularity: 1)),
		};

		var groups = MissedRelations.Group(list, withRelations);

		Assert.Equal<long[]>([[5, 8], [1, 2], [6]], groups.Select(g => g.Watched.Select(x => x.Id).Order().ToArray()));
		Assert.Equal<long[]>([[4], [3], [7]], groups.Select(g => g.Missed.Select(x => x.Id).ToArray()));
	}

	[Fact]
	public async Task WithRelations_WalksPastGapsInASeasonChain()
	{
		// 1 watched → 2 planned → 3 missed → 4 missed; 1 → 5 dropped → 6 not followed.
		var db = new Dictionary<long, AnimeModel>
		{
			[1] = new() { Id = 1, Related = [Related(2, AnimeRelationType.Sequel), Related(5, AnimeRelationType.Prequel)] },
			[2] = new() { Id = 2, Related = [Related(1, AnimeRelationType.Prequel), Related(3, AnimeRelationType.Sequel)] },
			[3] = new() { Id = 3, Related = [Related(2, AnimeRelationType.Prequel), Related(4, AnimeRelationType.Sequel)] },
			[4] = new() { Id = 4, Related = [Related(3, AnimeRelationType.Prequel)] },
			[5] = new() { Id = 5, Related = [Related(6, AnimeRelationType.Prequel)] },
		};
		var list = new[]
		{
			new AnimeModel { Id = 1, Tracking = new Tracking { Status = ListItemStatus.Completed } },
			new AnimeModel { Id = 2, Tracking = new Tracking { Status = ListItemStatus.PlanToWatch } },
			new AnimeModel { Id = 5, Tracking = new Tracking { Status = ListItemStatus.Dropped } },
		};

		List<long[]> steps = [];
		var withRelations = await MissedRelations.WithRelationsAsync(list, ids => Task.FromResult(ids.Select(x => db[x]).ToList()),
																	 soFar => steps.Add([.. soFar.Select(x => x.Id)]));

		Assert.Equal([1, 2, 3, 4], withRelations.Select(x => x.Id));
		Assert.Equal<long[]>([[1], [1, 2], [1, 2, 3], [1, 2, 3, 4]], steps);
		Assert.Equal([3, 4], MissedRelations.Find(list, withRelations).Select(x => x.Id).Order());
	}

	[Fact]
	public void IsWatched_CountsCompletedWatchingAndRewatching()
	{
		Assert.True(MissedRelations.IsWatched(new AnimeModel { Tracking = new Tracking { Status = ListItemStatus.Rewatching } }));
		Assert.False(MissedRelations.IsWatched(new AnimeModel { Tracking = new Tracking { Status = ListItemStatus.Dropped } }));
		Assert.False(MissedRelations.IsWatched(new AnimeModel()));
	}
}
