using System.ComponentModel;
using Totoro.Core.Agent;
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
