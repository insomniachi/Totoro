using Microsoft.UI.Xaml;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// How many episodes of an anime the user is watching have aired that they haven't watched, as a badge (Fluent's
/// InfoBadge, as for new items) on its card's top right: while it airs, and for a while after it's finished (see
/// <see cref="EpisodeAvailability"/>), and while the setting's on (see <see cref="CardPreferences"/>).
/// </summary>
public sealed class NewEpisodesOverlay : LiveAnimeCardOverlay<NewEpisodesBadge>
{
	protected override NewEpisodesBadge Create() => new();

	protected override void Update(NewEpisodesBadge badge, AnimeModel? anime, IPreferences? preferences, DateTime now)
	{
		var count = anime is not null && (preferences?.ShowNewEpisodesOnCards ?? true) ? EpisodeAvailability.NewEpisodes(anime, now) : 0;
		badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
		if (count > 0)
		{
			badge.Set(count);
		}
	}
}
