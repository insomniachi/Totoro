using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Services;

/// <summary>Marks an episode that starts or ends a season.</summary>
public enum ReleaseMarker
{
	None,
	Premiere,
	Finale,
}

/// <summary>An episode of an anime the user is watching, and when it airs (local time).</summary>
public sealed record ScheduledEpisode(AnimeModel Anime, int Number, DateTime AiringAt, ReleaseMarker Marker)
{
	public bool IsWatched => Anime.Tracking?.WatchedEpisodes >= Number;
}

/// <summary>When the episodes of the anime the user is watching air, for the release calendar and agent tools alike.</summary>
public sealed class ReleaseScheduleService(IFactory<IAnimeTracker, Guid> trackers)
{
	/// <summary>The anime the user is watching (or rewatching), from their list in the selected database.</summary>
	public async Task<List<AnimeModel>> GetWatchingAsync()
	{
		var tracker = trackers.CreateDefault() ?? throw new InvalidOperationException("No anime database is set up.");
		var list = await tracker.GetUserListAsync(CancellationToken.None);
		return [.. list.Where(x => x.Tracking?.Status is ListItemStatus.Watching or ListItemStatus.Rewatching)];
	}

	/// <summary>The episodes of <paramref name="anime"/> airing from <paramref name="from"/> up to (not including) <paramref name="to"/>, in order.</summary>
	public static List<ScheduledEpisode> Between(IEnumerable<AnimeModel> anime, DateTime from, DateTime to)
	{
		return [.. anime.SelectMany(x => AiringTimes(x, to).Where(e => e.At >= from && e.At < to)
															.Select(e => new ScheduledEpisode(x, e.Number, e.At, MarkerOf(x, e.Number))))
						.OrderBy(x => x.AiringAt)];
	}

	private static ReleaseMarker MarkerOf(AnimeModel anime, int number) => number switch
	{
		1 => ReleaseMarker.Premiere,
		_ when number == anime.TotalEpisodes => ReleaseMarker.Finale,
		_ => ReleaseMarker.None,
	};

	private static IEnumerable<(int Number, DateTime At)> AiringTimes(AnimeModel anime, DateTime until)
	{
		if (anime.AiringSchedule.Count > 0)
		{
			return anime.AiringSchedule.Select(x => (x.Key, x.Value));
		}

		// shortcut: databases without a schedule (MyAnimeList) only give the next episode; others are a week apart from it,
		// ignoring breaks. Upgrade if one of them gets per-episode air dates.
		if (anime.NextEpisodeAt is not { } next)
		{
			return [];
		}

		var nextNumber = anime.AiredEpisodes + 1;
		var last = anime.TotalEpisodes ?? nextNumber + (int)Math.Max(0, (until - next).TotalDays / 7);
		return Enumerable.Range(1, Math.Max(0, last)).Select(n => (n, next.AddDays(7 * (n - nextNumber))));
	}
}
