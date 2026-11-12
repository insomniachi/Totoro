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
	public void IsWatched_CountsCompletedWatchingAndRewatching()
	{
		Assert.True(MissedRelations.IsWatched(new AnimeModel { Tracking = new Tracking { Status = ListItemStatus.Rewatching } }));
		Assert.False(MissedRelations.IsWatched(new AnimeModel { Tracking = new Tracking { Status = ListItemStatus.Dropped } }));
		Assert.False(MissedRelations.IsWatched(new AnimeModel()));
	}
}
