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
	/// The watched anime of <paramref name="list"/> with their relations, from <paramref name="fetch"/>; and down each
	/// sequel and prequel chain from them, the seasons not watched (missed, planned or on hold) with theirs, so every
	/// season past a gap is found too, not just the next one. A fetch per 50 anime per step down the chains, after each of which
	/// <paramref name="onStep"/> gets what's been fetched so far.
	/// </summary>
	public static async Task<List<AnimeModel>> WithRelationsAsync(IReadOnlyCollection<AnimeModel> list,
		Func<IReadOnlyCollection<long>, Task<List<AnimeModel>>> fetch, Action<IReadOnlyCollection<AnimeModel>>? onStep = null)
	{
		var status = list.DistinctBy(x => x.Id).ToDictionary(x => x.Id, x => x.Tracking?.Status);
		var seen = list.Where(IsWatched).Select(x => x.Id).ToHashSet();
		List<AnimeModel> all = [];
		// shortcut: at most 30 seasons down a chain, raise it if a franchise is cut short.
		IReadOnlyCollection<long> next = [.. seen];
		for (var round = 0; next.Count > 0 && round < 30; round++)
		{
			List<AnimeModel> fetched = [];
			// 50 at a time, a request each, so what's found shows as it comes.
			foreach (var chunk in next.Chunk(50))
			{
				fetched.AddRange(await fetch(chunk));
				onStep?.Invoke([.. all, .. fetched]);
			}

			all.AddRange(fetched);
			next = [.. fetched.SelectMany(x => x.Related)
							  .Where(x => x.Relation is AnimeRelationType.Sequel or AnimeRelationType.Prequel && x.Id > 0
										  && x.AiringStatus is not AiringStatus.NotYetAired
										  && status.GetValueOrDefault(x.Id) is null or ListItemStatus.PlanToWatch or ListItemStatus.OnHold)
							  .Select(x => x.Id)
							  .Where(seen.Add)];
		}

		return all;
	}

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

	/// <summary>Whether the page shows it open, its missed anime under it.</summary>
	public bool IsExpanded { get; init; } = true;
}
