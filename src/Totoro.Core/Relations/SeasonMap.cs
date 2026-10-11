using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Relations;

/// <summary>
/// A series' seasons, first to last, found by following prequels and sequels in the anime database: what a provider's
/// absolute numbering counts through when it has one show for the whole series. Only TV-like entries (TV, ONA) count as
/// seasons; movies, OVAs and specials, which providers number apart, are left out.
/// </summary>
public static class SeasonMap
{
	// Enough for long series, and a stop for loops in the data.
	private const int MaxSeasons = 20;

	/// <summary>
	/// The seasons of the series <paramref name="anime"/> belongs to, first to last, including it (with each one's full
	/// details: its episode count, its ids). Just the anime when it has no prequels or sequels.
	/// </summary>
	public static async Task<IReadOnlyList<AnimeModel>> GetSeasonsAsync(IAnimeMetadataProvider database, long animeId, CancellationToken ct)
	{
		var anime = await database.GetAnimeAsync(animeId);
		var seen = new HashSet<long> { anime.Id };
		List<AnimeModel> before = [];
		List<AnimeModel> after = [];

		for (var current = anime; before.Count + after.Count < MaxSeasons && Next(current, AnimeRelationType.Prequel, seen) is { } prequel;)
		{
			ct.ThrowIfCancellationRequested();
			current = await database.GetAnimeAsync(prequel.Id);
			before.Insert(0, current);
		}

		for (var current = anime; before.Count + after.Count < MaxSeasons && Next(current, AnimeRelationType.Sequel, seen) is { } sequel;)
		{
			ct.ThrowIfCancellationRequested();
			current = await database.GetAnimeAsync(sequel.Id);
			after.Add(current);
		}

		return [.. before, anime, .. after];
	}

	/// <summary>
	/// The absolute number of season <paramref name="seasonIndex"/>'s first episode, counting from season
	/// <paramref name="fromIndex"/>'s first; null if a season in between hasn't a known length.
	/// </summary>
	public static int? AbsoluteFirstEpisode(IReadOnlyList<AnimeModel> seasons, int fromIndex, int seasonIndex)
	{
		var first = 1;
		for (var i = fromIndex; i < seasonIndex; i++)
		{
			if (seasons[i].TotalEpisodes is not { } episodes || episodes <= 0)
			{
				return null;
			}

			first += episodes;
		}

		return first;
	}

	private static AnimeModel? Next(AnimeModel anime, AnimeRelationType relation, HashSet<long> seen)
	{
		var next = anime.Related.FirstOrDefault(x => x.Relation == relation && IsSeason(x) && !seen.Contains(x.Id));
		if (next is not null)
		{
			seen.Add(next.Id);
		}

		return next;
	}

	private static bool IsSeason(AnimeModel anime) => anime.MediaFormat is AnimeMediaFormat.Tv or AnimeMediaFormat.Ona or AnimeMediaFormat.Unknown;
}
