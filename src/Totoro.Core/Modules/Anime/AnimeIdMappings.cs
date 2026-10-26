using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;

namespace Totoro.Core.Modules.Anime;

public static class AnimeIdMappings
{
	extension(AnimeModel anime)
	{
		/// <summary>The ani.zip query parameter its own id goes in, e.g. "mal_id"; null for a database ani.zip doesn't map.</summary>
		internal string? AniZipKey => anime.ServiceName switch
		{
			nameof(AnimeId.Anilist) => "anilist_id",
			nameof(AnimeId.MyAnimeList) or nameof(AnimeId.Shikimori) => "mal_id",
			nameof(AnimeId.Kitsu) => "kitsu_id",
			_ => null,
		};

		/// <summary>
		/// The anime's id in another database, e.g. its AniList id for one from MyAnimeList: from its external ids, or else
		/// looked up on ani.zip (which maps them) and kept in them. Null if that database hasn't it.
		/// </summary>
		/// <param name="service">The database, as an <see cref="AnimeId"/> property's name, e.g. "Anilist".</param>
		public async Task<long?> FindIdAsync(string service, CancellationToken ct = default)
		{
			if (anime.ExternalIds.GetIdForService(service) is > 0 and var known)
			{
				return known;
			}

			var ownId = anime.AniZipKey;
			if (ownId is null)
			{
				return null;
			}

			var response = await "https://api.ani.zip/mappings".SetQueryParam(ownId, anime.Id).GetStringAsync(cancellationToken: ct);
			if (JsonNode.Parse(response)?["mappings"] is not { } mappings)
			{
				return null;
			}

			anime.ExternalIds.Anilist = Positive(mappings["anilist_id"]) ?? anime.ExternalIds.Anilist;
			anime.ExternalIds.MyAnimeList = Positive(mappings["mal_id"]) ?? anime.ExternalIds.MyAnimeList;
			anime.ExternalIds.Kitsu = Positive(mappings["kitsu_id"]) ?? anime.ExternalIds.Kitsu;
			anime.ExternalIds.AniDb = Positive(mappings["anidb_id"]) ?? anime.ExternalIds.AniDb;
			return anime.ExternalIds.GetIdForService(service) is > 0 and var found ? found : null;
		}
	}

	private static long? Positive(JsonNode? node)
	{
		return node is JsonValue value && value.TryGetValue<long>(out var id) && id > 0 ? id : null;
	}
}
