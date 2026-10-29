using Microsoft.UI.Xaml;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// Whether an anime is airing or yet to air, as a coloured dot on its card's bottom left.
/// Finished anime, most of them, get none, nor do those showing a next episode countdown (NextEpisodeOverlay), which says as much.
/// </summary>
public sealed class AiringStatusOverlay : LiveAnimeCardOverlay<AiringStatusBadge>
{
	protected override AiringStatusBadge Create() => new();

	protected override void Update(AiringStatusBadge badge, AnimeModel? anime, DateTime now)
	{
		var shown = anime is { AiringStatus: not AiringStatus.FinishedAiring } && !NextEpisodeOverlay.IsShown(anime, now);
		badge.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
		if (shown)
		{
			badge.Set(anime!.AiringStatus);
		}
	}
}
