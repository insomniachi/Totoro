using Totoro.Playback;
using Xunit;

namespace Totoro.Tests;

public class SyncplayTests
{
	private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

	[Fact]
	public void Follow_InStep_DoesNothing()
	{
		Assert.Equal((null, null), SyncplayClient.Follow(S(100), false, S(101), false, false));
	}

	[Fact]
	public void Follow_RoomPaused_PausesAndSeeksToTheSameFrame()
	{
		Assert.Equal((true, S(110)), SyncplayClient.Follow(S(100), false, S(110), true, false));
	}

	[Fact]
	public void Follow_RoomResumed_ResumesWithoutSeekingWhenClose()
	{
		Assert.Equal((false, null), SyncplayClient.Follow(S(100), true, S(100.5), false, false));
	}

	[Fact]
	public void Follow_SomeoneSeeked_Seeks()
	{
		Assert.Equal((null, S(300)), SyncplayClient.Follow(S(100), false, S(300), false, true));
	}

	[Fact]
	public void Follow_SmallDrift_IsLeft_ButBigDriftCatchesUp()
	{
		Assert.Equal((null, null), SyncplayClient.Follow(S(100), false, S(103), false, false));
		Assert.Equal((null, S(106)), SyncplayClient.Follow(S(100), false, S(106), false, false));
	}

	[Fact]
	public void IsSeek_PlayingOn_IsNot()
	{
		Assert.False(SyncplayClient.IsSeek(S(10), false, S(0.25), S(10.3)));
	}

	[Fact]
	public void IsSeek_KeyframeSnapWhilePlaying_IsNot()
	{
		// The built-in player lands a seek a few seconds off: not the user seeking.
		Assert.False(SyncplayClient.IsSeek(S(607), false, S(0.25), S(604)));
	}

	[Fact]
	public void IsSeek_JumpWhilePlaying_Is()
	{
		Assert.True(SyncplayClient.IsSeek(S(10), false, S(0.25), S(60)));
		Assert.True(SyncplayClient.IsSeek(S(60), false, S(0.25), S(10)));
	}

	[Fact]
	public void IsSeek_Paused_CountsNoTimePassing()
	{
		Assert.False(SyncplayClient.IsSeek(S(10), true, S(30), S(10)));
		Assert.True(SyncplayClient.IsSeek(S(10), true, S(0.25), S(20)));
	}

	[Theory]
	[InlineData("syncplay.pl:8995", "syncplay.pl", 8995)]
	[InlineData(" example.org ", "example.org", 8999)]
	public void TryParseServer_Valid(string server, string host, int port)
	{
		Assert.True(SyncplayClient.TryParseServer(server, out var h, out var p));
		Assert.Equal((host, port), (h, p));
	}

	[Theory]
	[InlineData("")]
	[InlineData("host:notaport")]
	[InlineData("host:70000")]
	[InlineData("a:1:2")]
	public void TryParseServer_Invalid(string server)
	{
		Assert.False(SyncplayClient.TryParseServer(server, out _, out _));
	}
}
