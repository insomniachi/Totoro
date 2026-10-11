using System.Reactive.Linq;

namespace Totoro.Core.Filtering;

public static class FilterExtensions
{
	/// <summary>
	/// Combines filters into a single predicate stream; an item has to pass every filter.
	/// Re-emits whenever any of the filters changes.
	/// </summary>
	public static IObservable<Func<T, bool>> Combine<T>(this IEnumerable<IFilter<T>> filters)
	{
		var predicates = filters.Select(x => x.Predicate).ToList();

		if (predicates.Count == 0)
		{
			return Observable.Return<Func<T, bool>>(_ => true);
		}

		return predicates.CombineLatest(current => (Func<T, bool>)(item => current.All(predicate => predicate(item))));
	}
}
