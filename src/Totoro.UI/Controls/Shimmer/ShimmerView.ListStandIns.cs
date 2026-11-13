// Ported from Uno Toolkit 11.0.1's SkeletonView (https://github.com/unoplatform/uno.toolkit.ui), MIT, Copyright (c) 2021 Uno Platform.
// Changed: rows are filled with ShimmerView.PlaceholderItem rather than always null, which x:Bind templates throw on.
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Totoro.UI.Controls;

// Placeholder rows for empty lists in the content: an opted-in list (ShimmerView.PlaceholderCount) with an
// ItemTemplate and no items is stood in for, while loading, by a private copy of the list filled with placeholder
// items. The copy lives in an invisible host on the overlay, over the real list, and its rows are mirrored in
// place of the real list's. The real list is never reparented and its data is never touched: only its MinHeight
// is raised, when it is sized by its (missing) content, so the surrounding layout makes room for the rows.
public sealed partial class ShimmerView
{
	// Differences below this are layout rounding, not a geometry change; ignoring them keeps the
	// update -> layout -> update cycle from oscillating.
	private const double LayoutTolerance = 0.5;

	private sealed class ListStandIn(FrameworkElement placeholderList, int count)
	{
		public FrameworkElement PlaceholderList { get; } = placeholderList;

		public int Count { get; set; } = count;

		// Set once the real list's MinHeight has been raised to reserve the rows' space.
		public PropertyValue? OriginalMinHeight { get; set; }

		public double OriginalMinHeightValue { get; set; }
	}

	private readonly Dictionary<FrameworkElement, ListStandIn> _listStandIns = [];
	private Canvas? _standInHost;

	private void UpdateListStandIns(DependencyObject contentRoot)
	{
		if (_overlay is null) return;

		List<(FrameworkElement List, int Count)>? lists = null;
		FindPlaceholderLists(contentRoot, ref lists);

		// drop the stand-ins of lists that got items, left the content or lost their size
		foreach (var list in _listStandIns.Keys.Where(x => lists?.Exists(entry => ReferenceEquals(entry.List, x)) != true).ToList())
		{
			RemoveListStandIn(list);
		}

		foreach (var (list, count) in lists ?? [])
		{
			if (!_listStandIns.TryGetValue(list, out var standIn))
			{
				if (CreatePlaceholderList(list, count) is not { } placeholderList)
				{
					continue; // not a supported kind of list
				}

				// Placed at the real list's origin, which is inside its margin: an implicit style's margin would offset it.
				placeholderList.Margin = new Thickness(0);
				// never focusable (IsTabStop alone would leave its item containers as tab stops)
				if (placeholderList is Control control)
				{
					control.IsEnabled = false;
				}
				// null items leave their containers' DataContext to inheritance: without this, rows would bind
				// to the ShimmerView's DataContext (typically the page's model) instead of having empty values
				placeholderList.DataContext = null;

				standIn = new ListStandIn(placeholderList, count);
				_listStandIns[list] = standIn;
				EnsureStandInHost().Children.Add(placeholderList);
			}
			else if (!ShimmerView.GetFillView(list) && standIn.Count != count)
			{
				SetItemsSource(standIn.PlaceholderList, PlaceholderItems(list, count));
				standIn.Count = count;
			}

			UpdateStandInLayout(list, standIn);
			if (ShimmerView.GetFillView(list))
			{
				FillView(list, standIn, count);
			}
		}

		if (_listStandIns.Count == 0)
		{
			RemoveStandInHost();
		}
	}

	private static void FindPlaceholderLists(DependencyObject parent, ref List<(FrameworkElement List, int Count)>? results)
	{
		foreach (var child in parent.GetChildren())
		{
			if (child is not UIElement element || element.Visibility == Visibility.Collapsed) continue;

			if (element is FrameworkElement fe)
			{
				// same exclusions as placeholder collection: such elements are never walked into
				if (ShimmerView.GetIgnore(fe) || ShimmerView.GetShape(fe) != ShimmerShape.Auto || IsShimmerLeaf(fe) || fe is ShimmerView) continue;

				if (IsEmptyTemplatedList(fe))
				{
					if (fe.ActualWidth >= EmptySizeThreshold &&
						ShimmerView.TryGetInheritedPlaceholderCount(fe, out var count) &&
						count > 0)
					{
						(results ??= []).Add((fe, count));
					}
					continue;
				}
			}

			FindPlaceholderLists(child, ref results);
		}
	}

	private static bool IsEmptyTemplatedList(FrameworkElement element) => element switch
	{
		ItemsControl { ItemTemplate: { } } itemsControl => itemsControl.Items.Count == 0,
		ItemsRepeater { ItemTemplate: { } } itemsRepeater => (itemsRepeater.ItemsSourceView?.Count ?? 0) == 0,
		_ => false,
	};

	// The list's ShimmerView.PlaceholderItem in every row, or null rows (fine for {Binding} templates) when it has none.
	private static object?[] PlaceholderItems(FrameworkElement list, int count)
		=> Enumerable.Repeat(ShimmerView.GetPlaceholderItem(list), count).ToArray();

	private static FrameworkElement? CreatePlaceholderList(FrameworkElement list, int count)
	{
		// Same kind of list, so the rows get the same containers (and their padding/minimum height) as the real ones.
		// Unknown ItemsControl subclasses (ComboBox, FlipView, ...) are not mimicked.
		switch (list)
		{
			case ItemsRepeater itemsRepeater:
				return new ItemsRepeater
				{
					ItemTemplate = itemsRepeater.ItemTemplate,
					Layout = itemsRepeater.Layout is StackLayout stackLayout
						? new StackLayout { Orientation = stackLayout.Orientation, Spacing = stackLayout.Spacing }
						: itemsRepeater.Layout,
					ItemsSource = PlaceholderItems(list, count),
				};

			case ListViewBase listViewBase:
				var listCopy = listViewBase is GridView ? new StandInGridView() : (ListViewBase)new StandInListView();
				listCopy.SelectionMode = ListViewSelectionMode.None;
				listCopy.IsItemClickEnabled = false;
				return CopyItemsControl(listViewBase, listCopy, count);

			case ItemsControl itemsControl when itemsControl.GetType() == typeof(ItemsControl):
				return CopyItemsControl(itemsControl, new ItemsControl(), count);

			default:
				return null;
		}
	}

	private static ItemsControl CopyItemsControl(ItemsControl source, ItemsControl copy, int count)
	{
		// the copy is the base kind: a style targeting a subclass of the real list doesn't apply to it
		if (source.Style is { } style && style.TargetType?.IsAssignableFrom(copy.GetType()) == true)
		{
			copy.Style = style;
		}
		// The selectors are not copied: they can't be expected to handle placeholder items
		// (and ItemTemplate, required here, wins over ItemTemplateSelector anyway).
		copy.ItemTemplate = source.ItemTemplate;
		copy.ItemContainerStyle = source.ItemContainerStyle;
		copy.ItemsPanel = source.ItemsPanel;
		copy.Padding = source.Padding;
		copy.IsTabStop = false;
		copy.ItemsSource = PlaceholderItems(source, count);

		return copy;
	}

	// Bounds the rows a FillView list can grow to, should a layout report nonsense.
	private const int MaxFillCount = 200;

	/// <summary>
	/// Grows a <see cref="ShimmerView.FillViewProperty"/> list's rows to reach the visible edge, from the realized rows'
	/// stride: a line is a row of items for a horizontal StackLayout, a row of columns otherwise. Never shrinks them:
	/// rows past the edge are clipped anyway.
	/// </summary>
	private void FillView(FrameworkElement list, ListStandIn standIn, int count)
	{
		if (_overlay is null || GetItems(standIn.PlaceholderList) is not { Count: > 1 } items) return; // not laid out yet

		var horizontal = standIn.PlaceholderList is ItemsRepeater { Layout: StackLayout { Orientation: Orientation.Horizontal } };
		var origins = items.Select(x => x.TransformToVisual(_overlay).TransformPoint(default)).ToList();
		var first = origins[0];

		// Per line: its items, and how far on the next line starts.
		double stride;
		int perLine;
		if (horizontal)
		{
			stride = origins[1].X - first.X;
			perLine = 1;
		}
		else
		{
			perLine = origins.Count(x => Math.Abs(x.Y - first.Y) <= LayoutTolerance);
			if (perLine >= origins.Count) return; // one line realized: no stride to go by
			stride = origins[perLine].Y - first.Y;
		}
		if (stride < EmptySizeThreshold) return;

		var visible = GetVisibleArea(list);
		if (visible.IsEmpty) return;
		var start = list.TransformToVisual(_overlay).TransformPoint(default);
		var needed = horizontal ? visible.Right - start.X : visible.Bottom - start.Y;

		var target = Math.Clamp((int)Math.Ceiling(needed / stride) * perLine, count, MaxFillCount);
		if (target > standIn.Count)
		{
			SetItemsSource(standIn.PlaceholderList, PlaceholderItems(list, target));
			standIn.Count = target;
		}
	}

	// The placeholder list's realized items: an ItemsRepeater's children, an ItemsControl's panel's.
	private static List<UIElement>? GetItems(FrameworkElement placeholderList) => placeholderList switch
	{
		ItemsRepeater repeater => [.. repeater.GetChildren().OfType<UIElement>()],
		ItemsControl { ItemsPanelRoot: { } panel } => [.. panel.Children],
		_ => null,
	};

	private static void SetItemsSource(FrameworkElement placeholderList, object?[] items)
	{
		if (placeholderList is ItemsControl itemsControl)
		{
			itemsControl.ItemsSource = items;
		}
		else if (placeholderList is ItemsRepeater itemsRepeater)
		{
			itemsRepeater.ItemsSource = items;
		}
	}

	private void UpdateStandInLayout(FrameworkElement list, ListStandIn standIn)
	{
		if (_overlay is null) return;

		var placeholderList = standIn.PlaceholderList;
		var origin = list.TransformToVisual(_overlay).TransformPoint(default);
		if (!IsClose(Canvas.GetLeft(placeholderList), origin.X))
		{
			Canvas.SetLeft(placeholderList, origin.X);
		}
		if (!IsClose(Canvas.GetTop(placeholderList), origin.Y))
		{
			Canvas.SetTop(placeholderList, origin.Y);
		}
		if (!IsClose(placeholderList.Width, list.ActualWidth))
		{
			placeholderList.Width = list.ActualWidth;
		}

		ReserveRowsSpace(list, standIn);
	}

	private static void ReserveRowsSpace(FrameworkElement list, ListStandIn standIn)
	{
		var rowsHeight = standIn.PlaceholderList.ActualHeight;
		if (rowsHeight < EmptySizeThreshold || !double.IsNaN(list.Height)) return;

		if (standIn.OriginalMinHeight is null)
		{
			// Only a list sized by its content grows. One granted more space than it asked for (e.g. stretched
			// into a star row) keeps its bounds, and the rows that don't fit are clipped instead.
			if (LayoutInformation.GetLayoutSlot(list).Height > list.DesiredSize.Height + EmptySizeThreshold) return;
			if (list.ActualHeight >= rowsHeight - LayoutTolerance) return;

			standIn.OriginalMinHeight = PropertyValue.Capture(list, MinHeightProperty);
			standIn.OriginalMinHeightValue = list.MinHeight;
		}

		// MinHeight wins over MaxHeight in layout, so the cap is applied here; rows beyond it are clipped
		var minHeight = Math.Min(Math.Max(standIn.OriginalMinHeightValue, rowsHeight), list.MaxHeight);
		if (!IsClose(list.MinHeight, minHeight))
		{
			list.MinHeight = minHeight;
		}
	}

	private void CollectStandInPlaceholders(FrameworkElement list, ListStandIn standIn, List<PlaceholderInfo> results)
	{
		// rows are clipped to the space the layout granted the real list, and to what can be seen of it
		if (!TryGetLayoutSlotBounds(list, out var clip)) return;
		clip.Intersect(GetVisibleArea(list));
		if (clip.IsEmpty) return;

		var rows = new List<PlaceholderInfo>();
		CollectPlaceholder(standIn.PlaceholderList, rows);

		foreach (var row in rows)
		{
			var rect = row.Rect;
			rect.Intersect(clip);
			if (rect.IsEmpty || rect.Width < EmptySizeThreshold || rect.Height < EmptySizeThreshold) continue;

			// a clipped circle is no longer one
			results.Add(row with { Rect = rect, IsCircle = row.IsCircle && rect == row.Rect });
		}
	}

	private Canvas EnsureStandInHost()
	{
		if (_standInHost is null)
		{
			// invisible: only the placeholders mirrored from its rows are shown
			_standInHost = new Canvas { Opacity = 0, IsHitTestVisible = false };
			AutomationProperties.SetAccessibilityView(_standInHost, AccessibilityView.Raw);
			_overlay?.Children.Insert(0, _standInHost);
		}

		return _standInHost;
	}

	private void RemoveStandInHost()
	{
		if (_standInHost is null) return;

		_overlay?.Children.Remove(_standInHost);
		_standInHost = null;
	}

	private void RemoveListStandIn(FrameworkElement list)
	{
		if (!_listStandIns.Remove(list, out var standIn)) return;

		_standInHost?.Children.Remove(standIn.PlaceholderList);
		standIn.OriginalMinHeight?.Restore(list, MinHeightProperty);
	}

	private void RemoveListStandIns()
	{
		foreach (var (list, standIn) in _listStandIns)
		{
			standIn.OriginalMinHeight?.Restore(list, MinHeightProperty);
		}
		_listStandIns.Clear();
		RemoveStandInHost();
	}

	// The ListView/GridView copies, unseen by UI Automation: walking a copy's items realizes its containers, which
	// regenerates the overlay, which lays the copy out again, and the window hangs in that loop.
	private sealed partial class StandInGridView : GridView
	{
		protected override AutomationPeer OnCreateAutomationPeer() => new StandInAutomationPeer(this);
	}

	private sealed partial class StandInListView : ListView
	{
		protected override AutomationPeer OnCreateAutomationPeer() => new StandInAutomationPeer(this);
	}

	private sealed partial class StandInAutomationPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
	{
		protected override IList<AutomationPeer>? GetChildrenCore() => null;
	}

	private static bool IsClose(double value, double expected) => Math.Abs(value - expected) <= LayoutTolerance;

	private static bool IsClose(Windows.Foundation.Rect value, Windows.Foundation.Rect expected) =>
		IsClose(value.X, expected.X) && IsClose(value.Y, expected.Y) && IsClose(value.Width, expected.Width) && IsClose(value.Height, expected.Height);
}
