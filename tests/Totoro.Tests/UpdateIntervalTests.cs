using Totoro.Modules;
using Xunit;

namespace Totoro.Tests;

public class UpdateIntervalTests
{
	private static readonly DateTime s_now = new(2026, 11, 18, 12, 0, 0);

	[Fact]
	public void Never_IsNeverDue() => Assert.False(UpdateInterval.Never.IsDue(DateTime.MinValue, s_now));

	[Fact]
	public void Weekly_IsDueOnceAWeekHasPassed()
	{
		Assert.False(UpdateInterval.Weekly.IsDue(s_now.AddDays(-7).AddMinutes(1), s_now));
		Assert.True(UpdateInterval.Weekly.IsDue(s_now.AddDays(-7), s_now));
	}

	[Fact]
	public void NeverChecked_IsDue() => Assert.True(UpdateInterval.Monthly.IsDue(DateTime.FromFileTime(0), s_now));
}
