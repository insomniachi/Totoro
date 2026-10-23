using Microsoft.UI.Xaml;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// How many episodes of an anime the user is watching they've watched, out of its total (e.g. "5/12", or "5/?" while the
/// total isn't known), on its card's bottom right.
/// </summary>
public sealed class WatchProgressOverlay : LiveAnimeCardOverlay<LabelBadge>
{
	protected override LabelBadge Create() => new(HorizontalAlignment.Right, VerticalAlignment.Bottom);

	protected override void Update(LabelBadge badge, AnimeModel? anime, DateTime now)
	{
		var shown = anime?.Tracking?.Status is ListItemStatus.Watching or ListItemStatus.Rewatching;
		badge.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
		if (shown)
		{
			var watched = anime!.Tracking!.WatchedEpisodes ?? 0;
			var total = anime.TotalEpisodes is > 0 ? anime.TotalEpisodes.ToString() : "?";
			badge.Set("", $"{watched}/{total}", $"Watched {watched} of {total} episodes");
		}
	}
}
