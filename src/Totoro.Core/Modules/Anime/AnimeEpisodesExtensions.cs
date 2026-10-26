using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Flurl;
using Flurl.Http;

namespace Totoro.Core.Modules.Anime;

public static class AnimeEpisodesExtensions
{
	// By MyAnimeList id. Only lookups that worked, so a failed one (e.g. hitting Tenrai's rate limit) is tried again.
	private static readonly ConcurrentDictionary<long, IReadOnlySet<int>> s_fillers = new();

	extension(AnimeModel anime)
	{
		/// <summary>
		/// The anime's aired episodes (titles, air dates, thumbnails...) from ani.zip, which maps AniList and MyAnimeList ids.
		/// Episode numbers are rebased to start at 1; specials and fillers are flagged. Returns an empty list on failure.
		/// </summary>
		public async Task<List<EpisodeInfo>> GetEpisodesAsync(CancellationToken ct = default)
		{
			try
			{
				var serviceType = anime.AniZipKey ?? throw new NotSupportedException($"Service type {anime.ServiceName} is not supported.");

				var today = TimeProvider.System.GetUtcNow();
				var fillers = anime.GetFillersAsync(ct);

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

				var fillerNumbers = await fillers;
				foreach (var episode in regular)
				{
					episode.IsFiller = fillerNumbers.Contains(episode.EpisodeNumber);
				}

				return [.. result.OrderBy(x => x.IsSpecial).ThenBy(x => x.EpisodeNumber)];
			}
			catch (Exception e)
			{
				Debug.WriteLine(e);
				return [];
			}
		}

		/// <summary>
		/// The numbers of the anime's filler episodes, from MyAnimeList's episode list (through Tenrai, which has Jikan's API), numbered from 1 like
		/// <see cref="GetEpisodesAsync"/>'s. Empty if it has none, isn't on MyAnimeList, or they couldn't be fetched.
		/// </summary>
		public async Task<IReadOnlySet<int>> GetFillersAsync(CancellationToken ct = default)
		{
			var malId = anime.ExternalIds.MyAnimeList > 0 ? anime.ExternalIds.MyAnimeList
				: anime.ServiceName == nameof(AnimeId.MyAnimeList) ? anime.Id : 0;
			if (malId == 0)
			{
				return new HashSet<int>();
			}

			if (s_fillers.TryGetValue(malId, out var cached))
			{
				return cached;
			}

			try
			{
				var fillers = new HashSet<int>();
				for (var page = 1; ; page++)
				{
					if (page > 1)
					{
						// Tenrai allows 4 requests a second without a key.
						await Task.Delay(300, ct);
					}

					// Episodes wait for this: short, so an unreachable Tenrai only loses the filler flags.
					var response = await $"https://api.tenrai.org/v1/anime/{malId}/episodes".SetQueryParam("page", page).WithTimeout(10)
																							 .GetStringAsync(cancellationToken: ct);
					var json = JsonNode.Parse(response)!;
					foreach (var episode in json["data"]!.AsArray())
					{
						// mal_id is the episode's number.
						if (episode?["filler"]?.GetValue<bool>() == true)
						{
							fillers.Add(episode["mal_id"]!.GetValue<int>());
						}
					}

					if (json["pagination"]?["has_next_page"]?.GetValue<bool>() != true)
					{
						break;
					}
				}

				return s_fillers[malId] = fillers;
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				Debug.WriteLine(e);
				return new HashSet<int>();
			}
		}
	}
}
