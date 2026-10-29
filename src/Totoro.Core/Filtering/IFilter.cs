namespace Totoro.Core.Filtering;

/// <summary>
/// One criterion applied to a list of <typeparamref name="T"/>.
/// <see cref="Predicate"/> emits the current predicate on subscribe and a new one whenever the criterion changes,
/// so it can be fed straight into DynamicData's <c>Filter(IObservable&lt;Func&lt;T, bool&gt;&gt;)</c>.
/// </summary>
public interface IFilter<in T>
{
	IObservable<Func<T, bool>> Predicate { get; }
}
