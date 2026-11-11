namespace Totoro.Playback;

/// <summary>The rules <see cref="SyncplayClient"/> keeps the player in step with the room by.</summary>
internal static class SyncplaySync
{
	// While paused (or told someone seeked), this far from where it should be is a seek.
	private static readonly TimeSpan s_seekThreshold = TimeSpan.FromSeconds(1.5);

	// While playing, this far behind or ahead of the room it seeks to where the room is (Syncplay's own rewind threshold),
	// and a jump this far is the user seeking.
	private static readonly TimeSpan s_desyncThreshold = TimeSpan.FromSeconds(4);

	// Playing and not moving this long is loading, not the user's doing.
	private const double StallSeconds = 2;

	// Syncplay's own: slower by this much from this far ahead of the room (seconds), till this close.
	private const double SlowdownRate = 0.95;
	private const double SlowdownAfter = 1.5;
	private const double SlowdownUntil = 0.1;

	/// <summary>
	/// Whether their start (<paramref name="theirs"/>, by <paramref name="by"/>) came after ours, the name settling a tie: each
	/// side compares the same two, so exactly one follows the other, however far apart their clocks. Unknown (0) never is.
	/// </summary>
	internal static bool StartedAfter(long theirs, string by, long ours, string me) =>
		theirs > 0 && (theirs > ours || theirs == ours && string.CompareOrdinal(by, me) < 0);

	/// <summary>
	/// Whether the player's stuck loading: playing, yet not moved on for <paramref name="stillFor"/> seconds. Not near
	/// the end, where a player can sit before it says it's done.
	/// </summary>
	internal static bool IsStalled(bool paused, double stillFor, TimeSpan position, TimeSpan duration) =>
		!paused && stillFor > StallSeconds && duration - position > TimeSpan.FromSeconds(5);

	/// <summary>The speed to play at, <paramref name="ahead"/> seconds ahead of the room (null to keep it).</summary>
	internal static double? Slowdown(double ahead, bool slowed) =>
		!slowed && ahead > SlowdownAfter ? SlowdownRate : slowed && ahead < SlowdownUntil ? 1 : null;

	/// <summary>What to do to the player to be where the room is: pause or resume it (null to leave it), and where to seek (null to stay).</summary>
	internal static (bool? Pause, TimeSpan? SeekTo) Follow(TimeSpan local, bool localPaused, TimeSpan room, bool roomPaused, bool doSeek)
	{
		var apart = (room - local).Duration();
		var seek = apart > (doSeek || roomPaused ? s_seekThreshold : s_desyncThreshold);
		return (roomPaused != localPaused ? roomPaused : null, seek ? room : null);
	}

	/// <summary>Whether the player got to <paramref name="position"/> by seeking, rather than by playing (or staying paused) since the last check.</summary>
	internal static bool IsSeek(TimeSpan last, bool lastPaused, TimeSpan elapsed, TimeSpan position)
	{
		var expected = lastPaused ? last : last + elapsed;
		// Playing, only a jump the room wouldn't count as in step: the built-in player lands a seek on a keyframe a few
		// seconds off, and a stream can stall, neither of which is the user's doing.
		return (position - expected).Duration() > (lastPaused ? s_seekThreshold : s_desyncThreshold);
	}
}
