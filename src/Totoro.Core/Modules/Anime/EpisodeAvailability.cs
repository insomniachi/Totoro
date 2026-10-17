namespace Totoro.Core.Modules.Anime;

/// <summary>Which episodes of an anime have aired, by its schedule (not whether a provider has them yet).</summary>
public static class EpisodeAvailability
{
	/// <summary>How long after it finishes airing an anime's unwatched episodes still count as new.</summary>
	public static readonly TimeSpan RecentlyFinished = TimeSpan.FromDays(28);

	/// <summary>How many episodes have aired by <paramref name="now"/>; null when it isn't known.</summary>
	public static int? AiredEpisodes(AnimeModel anime, DateTime now)
	{
		return anime.AiringStatus switch
		{
			// The count is from when the list was loaded: the next episode may have aired since.
			AiringStatus.CurrentlyAiring => anime.AiredEpisodes + (anime.NextEpisodeAt <= now ? 1 : 0),
			AiringStatus.FinishedAiring => anime.TotalEpisodes ?? (anime.AiredEpisodes > 0 ? anime.AiredEpisodes : null),
			_ => null,
		};
	}

	/// <summary>
	/// The episodes of an anime the user is watching that have aired but they haven't watched: while it airs, and for a
	/// while (<see cref="RecentlyFinished"/>) after it's finished. 0 otherwise.
	/// </summary>
	public static int NewEpisodes(AnimeModel anime, DateTime now)
	{
		if (anime.Tracking is not { Status: ListItemStatus.Watching } tracking)
		{
			return 0;
		}

		var isRecent = anime.AiringStatus == AiringStatus.CurrentlyAiring ||
					   anime is { AiringStatus: AiringStatus.FinishedAiring, EndDate: { } end } && now - end <= RecentlyFinished;
		if (!isRecent || AiredEpisodes(anime, now) is not { } aired)
		{
			return 0;
		}

		return Math.Max(0, aired - (tracking.WatchedEpisodes ?? 0));
	}
}
