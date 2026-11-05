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

	[Fact]
	public void FirstSentence_KeepsTheReason_DropsTheAdvice()
	{
		Assert.Equal("Episode 5 isn't on Re:ANIME yet.",
			SyncplayClient.FirstSentence("Episode 5 isn't on Re:ANIME yet. If Re:ANIME uses absolute numbering, set that in its episode numbering."));
		Assert.Equal("Couldn't play it: timeout", SyncplayClient.FirstSentence("Couldn't play it: timeout"));
	}

	[Fact]
	public void FitChat_CutsToSyncplaysLimit()
	{
		Assert.Equal("short", SyncplayClient.FitChat("short"));
		var cut = SyncplayClient.FitChat(new string('a', 200));
		Assert.Equal(150, cut.Length);
		Assert.EndsWith("…", cut);
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

	[Fact]
	public void StartedAfter_TwoStartedAtOnce_ExactlyOneFollows()
	{
		// Each side asks about the other's start: one yes, one no, so they end up on one episode.
		Assert.True(SyncplayClient.StartedAfter(2000, "bob", 1000, "alice"));
		Assert.False(SyncplayClient.StartedAfter(1000, "alice", 2000, "bob"));

		// The same moment: the name settles it, the same way on both sides.
		Assert.True(SyncplayClient.StartedAfter(1000, "alice", 1000, "bob"));
		Assert.False(SyncplayClient.StartedAfter(1000, "bob", 1000, "alice"));
	}

	[Fact]
	public void IsStalled_PlayingButStuck_Is_ButNotPausedOrAtTheEnd()
	{
		Assert.True(SyncplayClient.IsStalled(false, 3, S(600), S(1440)));
		// A moment's hiccup, the user's pause, and the end (where a player sits before it's done) aren't.
		Assert.False(SyncplayClient.IsStalled(false, 1, S(600), S(1440)));
		Assert.False(SyncplayClient.IsStalled(true, 30, S(600), S(1440)));
		Assert.False(SyncplayClient.IsStalled(false, 3, S(1438), S(1440)));
	}

	[Fact]
	public void Slowdown_SlowsWhenALittleAhead_AndStopsOnceLevel()
	{
		Assert.Null(SyncplayClient.Slowdown(1, false));
		Assert.Equal(0.95, SyncplayClient.Slowdown(2, false));
		// Kept slow till nearly level (or behind), then normal.
		Assert.Null(SyncplayClient.Slowdown(1, true));
		Assert.Equal(1, SyncplayClient.Slowdown(0.05, true));
		Assert.Equal(1, SyncplayClient.Slowdown(-0.5, true));
	}

	[Fact]
	public void StartedAfter_UnknownStart_IsNot()
	{
		// An older Totoro doesn't say when: not followed within the moment after starting here, as before.
		Assert.False(SyncplayClient.StartedAfter(0, "alice", 1000, "bob"));
	}

	[Fact]
	public void ReconnectDelay_NeverInTheRoom_GivesUp()
	{
		Assert.Null(SyncplayClient.ReconnectDelay(false, 1));
	}

	[Fact]
	public void ReconnectDelay_LostAfterJoining_RetriesSoonerFirst_ThenGivesUp()
	{
		Assert.Equal(S(1), SyncplayClient.ReconnectDelay(true, 0));
		Assert.Equal(S(2), SyncplayClient.ReconnectDelay(true, 1));
		Assert.Equal(S(30), SyncplayClient.ReconnectDelay(true, 4));
		Assert.Null(SyncplayClient.ReconnectDelay(true, 5));
	}
}
