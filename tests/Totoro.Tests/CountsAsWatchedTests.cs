using Totoro.Playback;
using Xunit;

namespace Totoro.Tests;

public class CountsAsWatchedTests
{
	private static readonly TimeSpan s_credits = TimeSpan.FromMinutes(2);

	[Fact]
	public void CountsAt_UnknownLength_IsNull()
	{
		Assert.Null(WatchService.CountsAt(TimeSpan.Zero, null, s_credits));
	}

	[Fact]
	public void CountsAt_NoEnding_IsCreditsBeforeTheEnd()
	{
		Assert.Equal(TimeSpan.FromMinutes(22), WatchService.CountsAt(TimeSpan.FromMinutes(24), null, s_credits));
	}

	[Fact]
	public void CountsAt_EndingKnown_IsWhereItStarts()
	{
		var ending = TimeSpan.FromMinutes(21.5);
		Assert.Equal(ending, WatchService.CountsAt(TimeSpan.FromMinutes(24), ending, s_credits));
	}

	[Fact]
	public void CountsAt_EndingInFirstHalf_IsIgnored()
	{
		// A cold-open "ending" (or a misplaced segment) mustn't count the episode watched early.
		Assert.Equal(TimeSpan.FromMinutes(22), WatchService.CountsAt(TimeSpan.FromMinutes(24), TimeSpan.FromMinutes(5), s_credits));
	}

	[Fact]
	public void CountsAt_ShorterThanTheCredits_IsTheEnd()
	{
		Assert.Equal(TimeSpan.FromMinutes(1), WatchService.CountsAt(TimeSpan.FromMinutes(1), null, s_credits));
	}
}
