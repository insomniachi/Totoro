using Totoro.Core.Modules.Anime;
using Xunit;

namespace Totoro.Tests;

public class EpisodeAvailabilityTests
{
	private static readonly DateTime s_now = new(2026, 10, 17, 12, 0, 0);

	private static AnimeModel Airing(int aired, int watched, ListItemStatus status = ListItemStatus.Watching, DateTime? next = null) => new()
	{
		AiringStatus = AiringStatus.CurrentlyAiring,
		AiredEpisodes = aired,
		NextEpisodeAt = next ?? s_now.AddDays(3),
		TotalEpisodes = 12,
		Tracking = new Tracking { Status = status, WatchedEpisodes = watched },
	};

	private static AnimeModel Finished(int total, int watched, DateTime end) => new()
	{
		AiringStatus = AiringStatus.FinishedAiring,
		TotalEpisodes = total,
		EndDate = end,
		Tracking = new Tracking { Status = ListItemStatus.Watching, WatchedEpisodes = watched },
	};

	[Fact]
	public void Airing_CountsAiredNotWatched()
	{
		Assert.Equal(2, EpisodeAvailability.NewEpisodes(Airing(aired: 5, watched: 3), s_now));
	}

	[Fact]
	public void Airing_CaughtUp_IsNone()
	{
		Assert.Equal(0, EpisodeAvailability.NewEpisodes(Airing(aired: 5, watched: 5), s_now));
	}

	[Fact]
	public void Airing_NextEpisodeAiredSinceLoading_Counts()
	{
		Assert.Equal(1, EpisodeAvailability.NewEpisodes(Airing(aired: 5, watched: 5, next: s_now.AddMinutes(-5)), s_now));
	}

	[Fact]
	public void NotWatching_IsNone()
	{
		Assert.Equal(0, EpisodeAvailability.NewEpisodes(Airing(aired: 5, watched: 0, status: ListItemStatus.PlanToWatch), s_now));
	}

	[Fact]
	public void FinishedRecently_CountsTheRest()
	{
		Assert.Equal(3, EpisodeAvailability.NewEpisodes(Finished(total: 12, watched: 9, end: s_now.AddDays(-10)), s_now));
	}

	[Fact]
	public void FinishedLongAgo_IsNone()
	{
		Assert.Equal(0, EpisodeAvailability.NewEpisodes(Finished(total: 12, watched: 9, end: s_now.AddDays(-60)), s_now));
	}

	[Fact]
	public void FinishedWithoutEndDate_IsNone()
	{
		var anime = Finished(total: 12, watched: 9, end: s_now);
		anime.EndDate = null;
		Assert.Equal(0, EpisodeAvailability.NewEpisodes(anime, s_now));
	}
}
