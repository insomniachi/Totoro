using Totoro.Core.Filtering;
using Totoro.Core.Navigation;

namespace Totoro.Core.Modules.Anime;

/// <summary>The list page's tabs, named for anime ("Watching") or manga ("Reading").</summary>
public sealed class ListStatusFilter(MediaMode kind = MediaMode.Anime) : SelectionFilter<IMediaModel, ListItemStatus>(
	[
		new(Label(ListItemStatus.Watching, kind), ListItemStatus.Watching, kind == MediaMode.Manga ? "Read" : "Play"),
		new(Label(ListItemStatus.PlanToWatch, kind), ListItemStatus.PlanToWatch, "Clock"),
		new(Label(ListItemStatus.Completed, kind), ListItemStatus.Completed, "Completed"),
		new(Label(ListItemStatus.OnHold, kind), ListItemStatus.OnHold, "Pause"),
		new(Label(ListItemStatus.Dropped, kind), ListItemStatus.Dropped, "Cancel"),
	],
	status => item => Matches(item.ListStatus, status))
{
	// Title case, as tabs: "Plan To Watch".
	private static string Label(ListItemStatus status, MediaMode kind) =>
		string.Join(' ', MediaText.Status(status, kind).Split(' ').Select(x => char.ToUpperInvariant(x[0]) + x[1..]));

	private static bool Matches(ListItemStatus? actual, ListItemStatus selected)
	{
		// Rewatching (rereading) has no tab of its own, so it shows up under Watching (Reading).
		return actual == selected || (selected == ListItemStatus.Watching && actual == ListItemStatus.Rewatching);
	}
}
