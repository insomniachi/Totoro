using Totoro.UI.Controls.AnimeCardOverlays;
using Xunit;

namespace Totoro.Tests;

public class NextEpisodeOverlayTests
{
	[Theory]
	[InlineData(2, 4, 30, "2d 4h")]
	[InlineData(3, 0, 10, "3d")]
	[InlineData(0, 5, 20, "5h 20m")]
	[InlineData(0, 2, 0, "2h")]
	[InlineData(0, 0, 40, "40m")]
	public void Format_ShowsTheTwoLargestUnits(int days, int hours, int minutes, string expected)
	{
		Assert.Equal(expected, NextEpisodeOverlay.Format(new TimeSpan(days, hours, minutes, 0)));
	}

	[Fact]
	public void Format_UnderAMinute_IsOneMinute()
	{
		Assert.Equal("1m", NextEpisodeOverlay.Format(TimeSpan.FromSeconds(20)));
	}
}
