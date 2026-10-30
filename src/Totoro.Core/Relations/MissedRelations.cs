using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Relations;

/// <summary>Sequels, prequels, side stories and spin-offs of anime the user watched that aren't on their list.</summary>
public static class MissedRelations
{
	private static readonly HashSet<AnimeRelationType> s_kinds =
	[
		AnimeRelationType.Sequel, AnimeRelationType.Prequel, AnimeRelationType.SideStory, AnimeRelationType.SpinOff,
	];

	/// <summary>Whether an entry on the list is one the user watched, so its relations count.</summary>
	public static bool IsWatched(AnimeModel anime) =>
		anime.Tracking?.Status is ListItemStatus.Completed or ListItemStatus.Watching or ListItemStatus.Rewatching;

	/// <summary>
	/// The related anime of <paramref name="withRelations"/> (watched anime, with <see cref="AnimeModel.Related"/>) that
	/// aren't anywhere on <paramref name="list"/> and have aired, once each, most popular first.
	/// </summary>
	public static IReadOnlyList<AnimeModel> Find(IReadOnlyCollection<AnimeModel> list, IEnumerable<AnimeModel> withRelations)
	{
		var onList = list.Select(x => x.Id).ToHashSet();
		return
		[
			.. withRelations.SelectMany(x => x.Related)
							.Where(x => x.Relation is { } relation && s_kinds.Contains(relation))
							// Id 0: one MyAnimeList has no id for.
							.Where(x => x.Id > 0 && !onList.Contains(x.Id) && x.Tracking?.Status is null && x.AiringStatus is not AiringStatus.NotYetAired)
							.DistinctBy(x => x.Id)
							.OrderByDescending(x => x.Popularity)
		];
	}
}
