// Ported from Uno Toolkit 11.0.1's SkeletonView (https://github.com/unoplatform/uno.toolkit.ui), MIT, Copyright (c) 2021 Uno Platform.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Totoro.UI.Controls;

/// <summary>Defines the placeholder shape that shimmer generation produces for an element.</summary>
public enum ShimmerShape
{
	/// <summary>Inferred from the element: an <c>Ellipse</c> becomes a circle, everything else a rounded rectangle.</summary>
	Auto,

	/// <summary>A rounded rectangle.</summary>
	Rectangle,

	/// <summary>A circle, sized to the smaller dimension of the element and centered within its bounds.</summary>
	Circle,
}

/// <summary>Attached properties that tune what a <see cref="ShimmerView"/> generates.</summary>
public sealed partial class ShimmerView
{
	/// <summary>
	/// How many placeholder rows an empty list (<c>ItemsControl</c>, <c>ListView</c>, <c>GridView</c>, <c>ItemsRepeater</c>)
	/// with an <c>ItemTemplate</c> gets while loading; applies to the element and its descendants. The nearest element
	/// with a value applies; a 0 set locally disables rows.
	/// </summary>
	public static readonly DependencyProperty PlaceholderCountProperty = DependencyProperty.RegisterAttached(
		"PlaceholderCount", typeof(int), typeof(ShimmerView), new PropertyMetadata(0));

	public static int GetPlaceholderCount(FrameworkElement element) => (int)element.GetValue(PlaceholderCountProperty);

	public static void SetPlaceholderCount(FrameworkElement element, int value) => element.SetValue(PlaceholderCountProperty, value);

	/// <summary>
	/// The item each placeholder row of this list is built from. Uno fills the rows with null, which compiled
	/// <c>x:Bind</c> templates can't take (their generated code throws on a null item): give such a list a blank item of
	/// its type here. Text it carries sizes the placeholder lines, so sample text gives realistic ones.
	/// </summary>
	public static readonly DependencyProperty PlaceholderItemProperty = DependencyProperty.RegisterAttached(
		"PlaceholderItem", typeof(object), typeof(ShimmerView), new PropertyMetadata(null));

	public static object? GetPlaceholderItem(FrameworkElement element) => element.GetValue(PlaceholderItemProperty);

	public static void SetPlaceholderItem(FrameworkElement element, object? value) => element.SetValue(PlaceholderItemProperty, value);

	/// <summary>
	/// Whether this list's placeholder rows fill the view it's seen in, whatever its size: rows are added (from
	/// <see cref="PlaceholderCountProperty"/>, the least there are) until they reach the visible edge, the right one for
	/// a horizontal StackLayout and the bottom one otherwise, and again when the view grows.
	/// </summary>
	public static readonly DependencyProperty FillViewProperty = DependencyProperty.RegisterAttached(
		"FillView", typeof(bool), typeof(ShimmerView), new PropertyMetadata(false));

	public static bool GetFillView(FrameworkElement element) => (bool)element.GetValue(FillViewProperty);

	public static void SetFillView(FrameworkElement element, bool value) => element.SetValue(FillViewProperty, value);

	/// <summary>Whether an element and its subtree are left out of shimmer generation.</summary>
	public static readonly DependencyProperty IgnoreProperty = DependencyProperty.RegisterAttached(
		"Ignore", typeof(bool), typeof(ShimmerView), new PropertyMetadata(false));

	public static bool GetIgnore(FrameworkElement element) => (bool)element.GetValue(IgnoreProperty);

	public static void SetIgnore(FrameworkElement element, bool value) => element.SetValue(IgnoreProperty, value);

	/// <summary>
	/// The placeholder shape forced onto an element. Anything but <see cref="ShimmerShape.Auto"/> also makes the element
	/// a leaf: one placeholder covers it and its subtree isn't visited.
	/// </summary>
	public static readonly DependencyProperty ShapeProperty = DependencyProperty.RegisterAttached(
		"Shape", typeof(ShimmerShape), typeof(ShimmerView), new PropertyMetadata(ShimmerShape.Auto));

	public static ShimmerShape GetShape(FrameworkElement element) => (ShimmerShape)element.GetValue(ShapeProperty);

	public static void SetShape(FrameworkElement element, ShimmerShape value) => element.SetValue(ShapeProperty, value);

	/// <summary>
	/// The <see cref="PlaceholderCountProperty"/> of the element or its nearest ancestor with a value: one set locally
	/// (an explicit 0 counts, and vetoes an ancestor's value) or coming from a <c>Style</c> (any non-zero value).
	/// </summary>
	internal static bool TryGetInheritedPlaceholderCount(FrameworkElement element, out int count)
	{
		for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
		{
			count = (int)current.GetValue(PlaceholderCountProperty);
			if (count != 0 || current.ReadLocalValue(PlaceholderCountProperty) != DependencyProperty.UnsetValue)
			{
				return true;
			}
		}

		count = 0;
		return false;
	}
}

internal static class VisualTreeChildren
{
	internal static IEnumerable<DependencyObject> GetChildren(this DependencyObject parent)
	{
		var count = VisualTreeHelper.GetChildrenCount(parent);
		for (var i = 0; i < count; i++)
		{
			yield return VisualTreeHelper.GetChild(parent, i);
		}
	}
}
