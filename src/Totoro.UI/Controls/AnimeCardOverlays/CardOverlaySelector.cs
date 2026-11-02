using Microsoft.Xaml.Interactivity;
using Totoro.Core.Modules;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// Which overlays a card shows for its item, like a DataTemplateSelector for the overlays: set on a page's cards
/// (<see cref="AnimeCard.Overlays"/>), so each card carries only the overlays its item needs. The card asks again when
/// it shows another item, when the item changes, and each minute, and keeps the overlays it already has (by type).
/// Overlays every card has (its actions, hover effects) stay in the card template's Interaction.Behaviors.
/// </summary>
public abstract class CardOverlaySelector
{
	/// <summary>The overlays a card needs for <paramref name="item"/> at <paramref name="now"/>; new instances, at most one of each type.</summary>
	public abstract IEnumerable<Behavior> Select(IMediaModel item, DateTime now);
}

/// <summary>Discover's cards: where each is on the list, and its score.</summary>
public sealed class BrowseOverlays : CardOverlaySelector
{
	public override IEnumerable<Behavior> Select(IMediaModel item, DateTime now)
	{
		if (ListStatusOverlay.IsShown(item))
		{
			yield return new ListStatusOverlay();
		}

		if (MeanScoreOverlay.IsShown(item))
		{
			yield return new MeanScoreOverlay();
		}
	}
}

/// <summary>Next season's cards: no score yet, so just where each is on the list.</summary>
public sealed class UpcomingOverlays : CardOverlaySelector
{
	public override IEnumerable<Behavior> Select(IMediaModel item, DateTime now)
	{
		if (ListStatusOverlay.IsShown(item))
		{
			yield return new ListStatusOverlay();
		}
	}
}

/// <summary>The user's list: what's coming, what's new, and how far they are.</summary>
public sealed class MediaListOverlays : CardOverlaySelector
{
	public override IEnumerable<Behavior> Select(IMediaModel item, DateTime now)
	{
		// Anime only: manga have no schedule or provider episodes.
		if (NextEpisodeOverlay.IsShown(item, now))
		{
			yield return new NextEpisodeOverlay();
		}

		if (NewEpisodesOverlay.IsShown(item, now))
		{
			yield return new NewEpisodesOverlay();
		}

		if (AiringStatusOverlay.IsShown(item))
		{
			yield return new AiringStatusOverlay();
		}

		if (WatchProgressOverlay.IsShown(item))
		{
			yield return new WatchProgressOverlay();
		}
	}
}

/// <summary>Search results: where each is on the list, its score, and whether it's still coming out.</summary>
public sealed class MediaSearchOverlays : CardOverlaySelector
{
	private readonly BrowseOverlays _browse = new();

	public override IEnumerable<Behavior> Select(IMediaModel item, DateTime now)
	{
		foreach (var overlay in _browse.Select(item, now))
		{
			yield return overlay;
		}

		if (AiringStatusOverlay.IsShown(item))
		{
			yield return new AiringStatusOverlay();
		}
	}
}

/// <summary>Missed relations: where each is on the list, its score, and its format and season.</summary>
public sealed class MissedRelationsOverlays : CardOverlaySelector
{
	private readonly BrowseOverlays _browse = new();

	public override IEnumerable<Behavior> Select(IMediaModel item, DateTime now)
	{
		foreach (var overlay in _browse.Select(item, now))
		{
			yield return overlay;
		}

		if (FormatSeasonOverlay.IsShown(item))
		{
			yield return new FormatSeasonOverlay();
		}
	}
}
