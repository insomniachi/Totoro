using Totoro.Core.Filtering;

namespace Totoro.Core.Modules.Anime;

public sealed class ListStatusFilter() : SelectionFilter<AnimeModel, ListItemStatus>(
	[
		new("Watching", ListItemStatus.Watching, "Play"),
		new("Plan To Watch", ListItemStatus.PlanToWatch, "Clock"),
		new("Completed", ListItemStatus.Completed, "Completed"),
		new("On Hold", ListItemStatus.OnHold, "Pause"),
		new("Dropped", ListItemStatus.Dropped, "Cancel"),
	],
	status => anime => Matches(anime.Tracking?.Status, status))
{
	private static bool Matches(ListItemStatus? actual, ListItemStatus selected)
	{
		// Rewatching has no tab of its own, so it shows up under Watching.
		return actual == selected || (selected == ListItemStatus.Watching && actual == ListItemStatus.Rewatching);
	}
}
