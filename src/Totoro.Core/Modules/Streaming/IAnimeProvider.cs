using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Modules.Streaming;

/// <summary>
/// A source of anime to watch (e.g. SubsPlease): finds a show, lists its episodes and the videos for each. Modules
/// register implementations keyed by their module id.
/// </summary>
public interface IAnimeProvider
{
	/// <summary>The module's id.</summary>
	Guid Id { get; }

	string Name { get; }

	/// <summary>
	/// Uses <paramref name="settings"/> instead of the module's settings, from now on and for this instance only, e.g.
	/// an anime's own. It's a settings object of the module's type (see IModuleSettings.CreateCopy). Providers without
	/// settings needn't implement it.
	/// </summary>
	void UseSettings(Settings.DataContainerConfig settings)
	{
	}

	/// <summary>Shows whose title matches <paramref name="query"/>, best match first.</summary>
	Task<List<ProviderShow>> SearchAsync(string query, CancellationToken ct);

	/// <summary>
	/// The show that is the anime with these ids (e.g. its AniList id), for providers that know their shows' ids in an
	/// anime database: tried before searching its titles. Null when it hasn't it; providers that can't find shows by id
	/// needn't implement it.
	/// </summary>
	Task<ProviderShow?> FindShowAsync(AnimeId ids, CancellationToken ct) => Task.FromResult<ProviderShow?>(null);

	/// <summary>The show's released episodes, in no particular order.</summary>
	Task<List<ProviderEpisode>> GetEpisodesAsync(string showId, CancellationToken ct);

	/// <summary>The ways to watch the episode (qualities, mirrors...), the provider's preferred one first.</summary>
	Task<List<VideoServer>> GetServersAsync(ProviderEpisode episode, CancellationToken ct);
}

/// <param name="Score">How well the title matched the search, from 0 to 1; 1 is an exact match.</param>
public sealed record ProviderShow(string Id, string Title, double Score = 0)
{
	/// <summary>The show's cover, a URL; "" when the provider has none. A property, not a parameter, so plugins built before it still load.</summary>
	public string Image { get; init; } = "";

	/// <summary>
	/// The show's ids in anime databases (AniList, MyAnimeList...), when the provider knows them: an anime is matched with
	/// it by them instead of by title. Null when it knows none.
	/// </summary>
	public AnimeId? Ids { get; init; }
}

/// <param name="Number">The episode number as the provider numbers it (with absolute numbering, it carries on from a previous season).</param>
public sealed record ProviderEpisode(string ShowId, string Id, float Number);

public enum VideoServerKind
{
	/// <summary>A URL a media player can open directly.</summary>
	Direct,

	/// <summary>A .torrent file or magnet link, streamed to the player by an <see cref="ITorrentStreamer"/>.</summary>
	Torrent,
}

/// <param name="Name">What to call it, e.g. "1080p".</param>
public sealed record VideoServer(string Name, Uri Url, VideoServerKind Kind = VideoServerKind.Direct)
{
	/// <summary>HTTP headers the player has to send with requests for the video.</summary>
	public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}
