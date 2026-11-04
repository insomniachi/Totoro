using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Playback;

namespace Totoro.Agent;

[Description("Watching together over Syncplay: everyone in the same room on the same server pauses, plays and seeks together.")]
public sealed record WatchTogetherStatus(
	[property: Description("Whether the app is in the room (or joining it).")]
	bool IsOn,
	[property: Description("What's happening, e.g. who it's watching with, or why it left.")]
	string Status,
	[property: Description("The Syncplay server, as host:port.")]
	string Server,
	string Room,
	[property: Description("The name the others see.")]
	string Name,
	[property: Description("The others in the room.")]
	IReadOnlyList<string> People,
	[property: Description("What catch_up_watch_together would play, e.g. \"alice is watching Frieren · Episode 5\"; empty when there's nothing to catch up with (nobody else is watching an episode, or one's playing here).")]
	string CatchUp);

/// <summary>Agent tools for watching together (see <see cref="SyncplayClient"/>).</summary>
public sealed class WatchTogetherTools(SyncplayClient syncplay)
{
	[AgentTool("get_watch_together")]
	[Description("Whether the app is in a watch-together room, with whom, and the room it joins.")]
	public WatchTogetherStatus GetWatchTogether() =>
		new(syncplay.IsOn, syncplay.Status, syncplay.Server, syncplay.Room, syncplay.Name, [.. syncplay.People], syncplay.CatchUpText);

	[AgentTool("join_watch_together")]
	[Description("Joins a watch-together room (leaving any it's in), so episodes stay in step with everyone in it: an episode anyone starts in Totoro starts for everyone (and one already playing when joining starts here, where they are), and pausing and seeking are shared.")]
	public async Task<WatchTogetherStatus> JoinWatchTogetherAsync(
		[Description("The room; the one last used when not given.")] string? room = null,
		[Description("The name the others see; the one last used when not given.")] string? name = null,
		[Description("The Syncplay server as host:port; the one last used (syncplay.pl:8999 at first) when not given.")] string? server = null)
	{
		if (!string.IsNullOrWhiteSpace(room))
		{
			syncplay.Room = room.Trim();
		}

		if (!string.IsNullOrWhiteSpace(name))
		{
			syncplay.Name = name.Trim();
		}

		if (!string.IsNullOrWhiteSpace(server))
		{
			syncplay.Server = server.Trim();
		}

		await syncplay.JoinAsync();
		if (!syncplay.IsOn)
		{
			throw new AgentToolException(syncplay.Status);
		}

		return GetWatchTogether();
	}

	[AgentTool("catch_up_watch_together")]
	[Description("In a watch-together room with nothing playing (e.g. after closing the player), plays the episode the others are watching, from where they are.")]
	public string CatchUpWatchTogether()
	{
		if (syncplay.CatchUpText is not { Length: > 0 } what)
		{
			throw new AgentToolException(syncplay.IsOn ? "There's nothing to catch up with: nobody else is watching an episode, or one's playing here." : "Not in a watch-together room: join one first.");
		}

		// Not waited on: it lasts as long as the episode.
		_ = syncplay.CatchUpAsync();
		return $"Catching up: {what}.";
	}

	[AgentTool("leave_watch_together")]
	[Description("Leaves the watch-together room.")]
	public string LeaveWatchTogether()
	{
		syncplay.IsOn = false;
		return "Left the room.";
	}
}
