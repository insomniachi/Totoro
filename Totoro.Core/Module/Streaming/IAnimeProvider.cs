namespace Totoro.Core.Module.Streaming;

/// <summary>
/// A source of anime to watch (e.g. SubsPlease): finds a show, lists its episodes and the videos for each. Modules
/// register implementations keyed by their module id.
/// </summary>
public interface IAnimeProvider
{
	/// <summary>The module's id.</summary>
	Guid Id { get; }

	string Name { get; }

	/// <summary>Shows whose title matches <paramref name="query"/>, best match first.</summary>
	Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct);

	/// <summary>The show's released episodes, in no particular order.</summary>
	Task<List<ProviderEpisode>> GetEpisodesAsync(string showId, CancellationToken ct);

	/// <summary>The ways to watch the episode (qualities, mirrors...), the provider's preferred one first.</summary>
	Task<List<VideoServer>> GetServersAsync(ProviderEpisode episode, CancellationToken ct);
}

/// <param name="Score">How well the title matched the search, from 0 to 1; 1 is an exact match.</param>
public sealed record ProviderShow(string Id, string Title, double Score = 0);

/// <param name="Number">The episode number as the provider numbers it (it may continue on from a previous season).</param>
public sealed record ProviderEpisode(string ShowId, string Id, float Number);

public enum VideoServerKind
{
	/// <summary>A URL a media player can open directly.</summary>
	Direct,

	/// <summary>A .torrent file; players stream it themselves (mpv with a webtorrent script).</summary>
	Torrent,
}

/// <param name="Name">What to call it, e.g. "1080p".</param>
public sealed record VideoServer(string Name, Uri Url, VideoServerKind Kind = VideoServerKind.Direct)
{
	/// <summary>HTTP headers the player has to send with requests for the video.</summary>
	public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}
