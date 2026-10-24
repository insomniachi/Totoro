using Microsoft.UI.Xaml;
using Totoro.Core;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// How many episodes of an anime the user is watching have aired that they haven't watched, as a badge (Fluent's
/// InfoBadge, as for new items) on its card's top right: while it airs, and for a while after it's finished (see
/// <see cref="EpisodeAvailability"/>). Once its provider's been checked (<see cref="AnimeModel.ProviderEpisodes"/>), the
/// count is of those the provider has; when it has none of them yet, the badge is grey, with the count that aired.
/// </summary>
public sealed class NewEpisodesOverlay : LiveAnimeCardOverlay<NewEpisodesBadge>
{
	protected override NewEpisodesBadge Create() => new();

	protected override void Update(NewEpisodesBadge badge, AnimeModel? anime, DateTime now)
	{
		var count = anime is not null ? EpisodeAvailability.NewEpisodes(anime, now) : 0;
		badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
		if (count > 0)
		{
			// Only an airing anime gets new episodes on its provider; this card's shown, so it's worth asking.
			if (anime!.AiringStatus == AiringStatus.CurrentlyAiring)
			{
				Locator.GetRequiredService<IProviderEpisodes>().Check(anime);
			}

			var onProvider = anime!.ProviderEpisodes is { } latest ? Math.Clamp(latest - (anime.Tracking?.WatchedEpisodes ?? 0), 0, count) : count;
			badge.Set(onProvider > 0 ? onProvider : count, onProvider > 0);
		}
	}
}
