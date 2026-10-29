using System.Globalization;
using Microsoft.UI.Xaml;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// The next episode of an anime being watched or planned, and how long until it airs (e.g. "Ep 4 in 5d 22h"), on its
/// card's top left, while there's one coming.
/// </summary>
public sealed class NextEpisodeOverlay : LiveAnimeCardOverlay<NextEpisodeBadge>
{
	protected override NextEpisodeBadge Create() => new();

	protected override void Update(NextEpisodeBadge badge, AnimeModel? anime, DateTime now)
	{
		var left = anime?.NextEpisodeAt - now;
		var shown = IsShown(anime, now);
		badge.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
		if (shown && anime is { NextEpisodeAt: { } at })
		{
			// The one after those aired (both databases count them: AniList from its schedule, MyAnimeList from the weekly slot).
			var episode = anime.AiredEpisodes + 1;
			badge.Set(episode, Format(left!.Value), $"Episode {episode} airs {at.ToString("ddd d MMM, HH:mm", CultureInfo.CurrentCulture)}");
		}
	}

	/// <summary>Whether the countdown is up for the anime (AiringStatusOverlay steps aside then: a countdown already says it's airing).</summary>
	internal static bool IsShown(AnimeModel? anime, DateTime now) =>
		anime?.Tracking?.Status is ListItemStatus.Watching or ListItemStatus.PlanToWatch && anime.NextEpisodeAt - now > TimeSpan.Zero;

	/// <summary>e.g. "2d 4h", "5h 20m", "40m".</summary>
	internal static string Format(TimeSpan left)
	{
		if (left.TotalDays >= 1)
		{
			return left.Hours > 0 ? $"{left.Days}d {left.Hours}h" : $"{left.Days}d";
		}

		if (left.TotalHours >= 1)
		{
			return left.Minutes > 0 ? $"{left.Hours}h {left.Minutes}m" : $"{left.Hours}h";
		}

		return $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))}m";
	}
}
