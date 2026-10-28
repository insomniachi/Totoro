using Totoro.Core.Modules.Anime;
using Totoro.Modules.AnimeDatabase.Anilist;
using Xunit;

namespace Totoro.Tests;

public class AnilistLocalListTests
{
	[Fact]
	public void KeepsTrackingAcrossRestarts()
	{
		var list = new AnilistLocalList();
		list.Update(1, new Tracking { Status = ListItemStatus.Watching, Score = 8 });
		list.Update(1, new Tracking { WatchedEpisodes = 3 });
		list.Update(2, new Tracking { Status = ListItemStatus.PlanToWatch });
		Assert.True(list.Remove(2));
		Assert.False(list.Remove(2));

		// A new instance reads the file, as after restarting the app.
		var entry = Assert.Single(new AnilistLocalList().GetAll());
		Assert.Equal(1, entry.Key);
		Assert.Equal(ListItemStatus.Watching, entry.Value.Status);
		Assert.Equal(8, entry.Value.Score);
		Assert.Equal(3, entry.Value.WatchedEpisodes);
	}
}
