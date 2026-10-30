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
	public void IsWatched_CountsCompletedWatchingAndRewatching()
	{
		Assert.True(MissedRelations.IsWatched(new AnimeModel { Tracking = new Tracking { Status = ListItemStatus.Rewatching } }));
		Assert.False(MissedRelations.IsWatched(new AnimeModel { Tracking = new Tracking { Status = ListItemStatus.Dropped } }));
		Assert.False(MissedRelations.IsWatched(new AnimeModel()));
	}
}
