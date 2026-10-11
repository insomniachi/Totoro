using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Playback;

namespace Totoro.Agent;

[Description("How playing an episode went.")]
public sealed record PlayResult(
	[property: Description("Whether it's playing (or still getting ready to).")]
	bool Started,
	[property: Description("What happened, e.g. \"Playing in MPV\", or why it can't play.")]
	string Message,
	[property: Description("When the anime isn't matched with a show on its provider yet: the shows it may be, best first. Pick one with set_provider_show, then play again.")]
	IReadOnlyList<ShowCandidate>? ChooseShow = null);

[Description("A show on an anime provider (where episodes are streamed from).")]
public sealed record ShowCandidate(
	string Id,
	string Title,
	[property: Description("How well its title matched the anime's, from 0 to 1; 1 is an exact match.")]
	double Score);

[Description("An audio or subtitle track of the episode playing.")]
public sealed record PlaybackTrack(
	MediaTrackKind Kind,
	[property: Description("What select_playback_track takes; -1 is \"no subtitles\".")]
	int Id,
	string Name,
	bool IsSelected);

[Description("The shows on the anime's provider it may be, and the one it's matched with, if any.")]
public sealed record ProviderShows(string Provider, ShowCandidate? Matched, IReadOnlyList<ShowCandidate> Candidates);

/// <summary>Agent tools for watching anime (see <see cref="WatchService"/>).</summary>
public sealed class PlaybackTools(AnimeLookup lookup, WatchService watch)
{
	// Long enough for a torrent to find peers.
	private static readonly TimeSpan s_startTimeout = TimeSpan.FromSeconds(90);

	[AgentTool("play_episode")]
	[Description("Plays an episode of an anime in the media player, resuming where the user left off. Playback continues on its own; " +
				 "it's marked watched when they finish it.")]
	public async Task<PlayResult> PlayEpisodeAsync(
		long animeId,
		[Description("The episode's number; leave it out for the next one the user hasn't watched.")] int? episode = null)
	{
		var anime = await lookup.GetAsync(animeId);
		var number = episode ?? WatchService.NextEpisode(anime);
		if (watch.GetProvider(anime) is not { } provider)
		{
			throw new AgentToolException("There's no anime provider to watch from.");
		}

		// The UI asks the user to pick the show when it's unclear; the agent picks instead (or asks them).
		var (show, candidates) = await watch.FindShowAsync(provider, anime, CancellationToken.None);
		if (show is null)
		{
			return new PlayResult(false, $"It isn't clear which {provider.Name} show {anime.Title} is.", [.. candidates.Select(ToCandidate)]);
		}

		var status = await watch.StartWatchingAsync(anime, number, s_startTimeout);
		var started = status.IsActive && status.Kind is PlaybackStatusKind.Playing or PlaybackStatusKind.Progress;
		return new PlayResult(started, status.IsActive ? status.Message : "It was cancelled.");
	}

	[AgentTool("get_playback_status")]
	[Description("What's playing, or being got ready to play, and how it's going.")]
	public PlaybackStatus GetPlaybackStatus() => watch.Status;

	[AgentTool("set_playback_paused")]
	[Description("Pauses or resumes the episode playing. Only in the built-in player.")]
	public async Task<string> SetPlaybackPausedAsync(bool paused)
	{
		await Control(() => watch.SetPausedAsync(paused));
		return paused ? "Paused." : "Playing.";
	}

	[AgentTool("seek_playback")]
	[Description("Jumps to a time in the episode playing.")]
	public async Task<string> SeekPlaybackAsync([Description("Where to jump to, in seconds from the start.")] double seconds)
	{
		var position = TimeSpan.FromSeconds(Math.Max(0, seconds));
		await Control(() => watch.SeekAsync(position));
		return $"Jumped to {position.ToString(@"h\:mm\:ss")}.";
	}

	[AgentTool("get_playback_tracks")]
	[Description("The audio and subtitle tracks of the episode playing, and which are selected. Only in the built-in player.")]
	public async Task<IReadOnlyList<PlaybackTrack>> GetPlaybackTracksAsync()
	{
		IReadOnlyList<MediaTrack> tracks = [];
		await Control(() =>
		{
			tracks = watch.GetTracks();
			return Task.CompletedTask;
		});
		return [.. tracks.Select(x => new PlaybackTrack(x.Kind, x.Id, x.Name, x.IsSelected))];
	}

	[AgentTool("select_playback_track")]
	[Description("Switches the audio or subtitles of the episode playing to a track from get_playback_tracks. Returns the tracks as they are now.")]
	public async Task<IReadOnlyList<PlaybackTrack>> SelectPlaybackTrackAsync(
		MediaTrackKind kind,
		[Description("The track's id; -1 turns subtitles off.")] int id)
	{
		await Control(() => watch.SelectTrackAsync(kind, id));
		return await GetPlaybackTracksAsync();
	}

	/// <summary>Controls playback, with the reason it can't (nothing playing, another player) for the agent.</summary>
	private static async Task Control(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (InvalidOperationException ex)
		{
			throw new AgentToolException(ex.Message);
		}
	}

	[AgentTool("find_provider_shows")]
	[Description("The shows on the anime's provider (where its episodes are streamed from) that may be the anime, and the one it's matched with.")]
	public async Task<ProviderShows> FindProviderShowsAsync(
		long animeId,
		[Description("A title to search the provider for instead of the anime's own titles.")] string? query = null)
	{
		var anime = await lookup.GetAsync(animeId);
		var provider = watch.GetProvider(anime) ?? throw new AgentToolException("There's no anime provider to watch from.");
		var candidates = string.IsNullOrWhiteSpace(query)
			? await WatchService.SearchShowsAsync(provider, anime, CancellationToken.None)
			: await provider.SearchAsync(query, CancellationToken.None);
		var (matched, _) = await watch.FindShowAsync(provider, anime, CancellationToken.None);
		return new ProviderShows(provider.Name, matched is null ? null : ToCandidate(matched), [.. candidates.Select(ToCandidate)]);
	}

	[AgentTool("set_provider_show")]
	[Description("Matches an anime with a show on its provider (from find_provider_shows), so its episodes are played from that show.")]
	public async Task<string> SetProviderShowAsync(long animeId, string showId, string showTitle)
	{
		var anime = await lookup.GetAsync(animeId);
		var provider = watch.GetProvider(anime) ?? throw new AgentToolException("There's no anime provider to watch from.");
		watch.SetShow(anime, provider, new ProviderShow(showId, showTitle, 1));
		return $"{anime.Title} is now played from \"{showTitle}\" on {provider.Name}.";
	}

	private static ShowCandidate ToCandidate(ProviderShow show) => new(show.Id, show.Title, Math.Round(show.Score, 2));
}
