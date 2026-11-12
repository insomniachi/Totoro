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

	/// <summary>
	/// <see cref="Find"/>'s anime by franchise: each with the watched entries of <paramref name="list"/> it's related to,
	/// joined with whatever those are related to, as a sequel locator shows them. Most popular franchise first.
	/// </summary>
	public static IReadOnlyList<MissedRelationGroup> Group(IReadOnlyCollection<AnimeModel> list, IReadOnlyCollection<AnimeModel> withRelations)
	{
		var missed = Find(list, withRelations);
		var watched = list.Where(IsWatched).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
		var missedIds = missed.Select(x => x.Id).ToHashSet();
		var parent = new Dictionary<long, long>();
		long Root(long id)
		{
			while (parent.TryGetValue(id, out var up) && up != id)
			{
				id = up;
			}
			return id;
		}

		foreach (var anime in withRelations)
		{
			foreach (var related in anime.Related.Where(x => watched.ContainsKey(x.Id) || missedIds.Contains(x.Id)))
			{
				var (a, b) = (Root(related.Id), Root(anime.Id));
				if (a != b)
				{
					parent[a] = b;
				}
			}
		}

		var watchedBy = watched.Values.ToLookup(x => Root(x.Id));
		return
		[
			.. missed.GroupBy(x => Root(x.Id))
					 .Select(g => new MissedRelationGroup([.. watchedBy[g.Key].OrderByDescending(x => x.Popularity)], [.. g]))
		];
	}
}

/// <summary>One franchise's missed anime, and the watched ones that are why.</summary>
public sealed record MissedRelationGroup(IReadOnlyList<AnimeModel> Watched, IReadOnlyList<AnimeModel> Missed)
{
	/// <summary>Its most popular watched entry's title, usually the franchise's first.</summary>
	public string Title => Watched.FirstOrDefault()?.Title ?? Missed[0].Title;
}
