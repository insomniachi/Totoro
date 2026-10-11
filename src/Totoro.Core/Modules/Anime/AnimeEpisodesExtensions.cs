using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;

namespace Totoro.Core.Modules.Anime;

public static class AnimeEpisodesExtensions
{
	extension(AnimeModel anime)
	{
		/// <summary>
		/// The anime's aired episodes (titles, air dates, thumbnails...) from ani.zip, which maps AniList and MyAnimeList ids.
		/// Episode numbers are rebased to start at 1; specials are flagged. Returns an empty list on failure.
		/// </summary>
		public async Task<List<EpisodeInfo>> GetEpisodesAsync(CancellationToken ct = default)
		{
			try
			{
				var serviceType = anime.ServiceName switch
				{
					nameof(AnimeId.Anilist) => "anilist_id",
					nameof(AnimeId.MyAnimeList) => "mal_id",
					_ => throw new NotSupportedException($"Service type {anime.ServiceName} is not supported.")
				};

				var today = TimeProvider.System.GetUtcNow();

				var response = await "https://api.ani.zip/mappings".SetQueryParam(serviceType, anime.Id).GetStringAsync(cancellationToken: ct);
				var episodesObj = JsonNode.Parse(response)!["episodes"]!.AsObject();

				var result = new List<EpisodeInfo>();

				foreach (var property in episodesObj)
				{
					if (property.Value.Deserialize<EpisodeInfo>() is not { } ep)
					{
						continue;
					}

					if (ep.AirDateUtc is null || ep.AirDateUtc > today)
					{
						continue;
					}

					if (property.Key.StartsWith('S'))
					{
						ep.IsSpecial = true;
					}

					result.Add(ep);
				}

				// ani.zip usually numbers episodes per season, but some entries continue a previous season's numbering
				// (e.g. 13, 14, ...); rebase regular episodes so they start at 1. Specials ("S1", "S2"...) reuse regular
				// episode numbers, so they're left alone and never used to work out the offset.
				var regular = result.Where(x => !x.IsSpecial).ToList();
				if (regular.Count > 0)
				{
					var offset = regular.Min(x => x.EpisodeNumber) - 1;
					foreach (var episode in regular)
					{
						episode.EpisodeNumber -= offset;
					}
				}

				return [.. result.OrderBy(x => x.IsSpecial).ThenBy(x => x.EpisodeNumber)];
			}
			catch (Exception e)
			{
				Debug.WriteLine(e);
				return [];
			}
		}
	}
}
