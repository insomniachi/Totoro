using Microsoft.UI.Xaml;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// Whether an anime is airing or yet to air, as a coloured dot on its card's bottom left, while the setting's on (see
/// <see cref="CardPreferences"/>). Finished anime, most of them, get none.
/// </summary>
public sealed class AiringStatusOverlay : LiveAnimeCardOverlay<AiringStatusBadge>
{
	protected override AiringStatusBadge Create() => new();

	protected override void Update(AiringStatusBadge badge, AnimeModel? anime, IPreferences? preferences, DateTime now)
	{
		var shown = anime is { AiringStatus: not AiringStatus.FinishedAiring } && (preferences?.ShowAiringStatusOnCards ?? true);
		badge.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
		if (shown)
		{
			badge.Set(anime!.AiringStatus);
		}
	}
}
