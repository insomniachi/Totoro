using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;

namespace Totoro.Core.Filtering;

/// <summary>Non-generic base so XAML templates can bind to an option (x:DataType can't be generic).</summary>
/// <remarks><see cref="IconKey"/> names an icon the UI layer resolves (see Totoro.UI.Icons).</remarks>
public abstract record FilterOption(string Label, string? IconKey)
{
	public sealed override string ToString() => Label;
}

public sealed record FilterOption<TValue>(string Label, TValue Value, string? IconKey = null) : FilterOption(Label, IconKey);

/// <summary>
/// A filter where exactly one of a fixed set of options is selected, e.g. a segmented button bar.
/// Bind the view's ItemsSource to <see cref="Options"/> and SelectedIndex to <see cref="SelectedIndex"/>.
/// </summary>
public partial class SelectionFilter<T, TValue> : ObservableObject, IFilter<T>
{
	public SelectionFilter(IEnumerable<FilterOption<TValue>> options, Func<TValue, Func<T, bool>> createPredicate)
	{
		Options = [.. options];
		Predicate = this.WhenValueChanged(x => x.SelectedIndex)
						.Select(index => index >= 0 && index < Options.Count
							? createPredicate(Options[index].Value)
							: _ => true);
	}

	public IReadOnlyList<FilterOption<TValue>> Options { get; }

	public IObservable<Func<T, bool>> Predicate { get; }

	[ObservableProperty]
	public partial int SelectedIndex { get; set; }
}
