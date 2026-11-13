// Ported from Uno Toolkit 11.0.1's SkeletonView (https://github.com/unoplatform/uno.toolkit.ui), MIT, Copyright (c) 2021 Uno Platform.
// Left out: Source (ILoadable), ShimmerPresenter, LoadingView's ProgressExtensions and the hot reload handler.
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Totoro.UI.Controls;

/// <summary>
/// Wraps content and, while <see cref="IsLoading"/>, replaces it visually with placeholders generated from the content's
/// own layout (one per text, image, shape, button or input), each a toolkit Shimmer. No shimmer shapes to draw.
/// An empty list gets placeholder rows from its ItemTemplate with <see cref="ShimmerView.PlaceholderCountProperty"/>
/// (and, for an <c>x:Bind</c> template, <see cref="ShimmerView.PlaceholderItemProperty"/>).
/// </summary>
[TemplatePart(Name = TemplateParts.ContentPresenter, Type = typeof(ContentPresenter))]
[TemplatePart(Name = TemplateParts.ShimmerOverlay, Type = typeof(Canvas))]
[TemplateVisualState(GroupName = VisualStateNames.ShimmerStates, Name = VisualStateNames.ShimmerVisible)]
[TemplateVisualState(GroupName = VisualStateNames.ShimmerStates, Name = VisualStateNames.ShimmerHidden)]
public sealed partial class ShimmerView : ContentControl
{
	private static class TemplateParts
	{
		public const string ContentPresenter = "PART_ContentPresenter";
		public const string ShimmerOverlay = "PART_ShimmerOverlay";
	}

	private static class VisualStateNames
	{
		public const string ShimmerStates = nameof(ShimmerStates);
		public const string ShimmerVisible = nameof(ShimmerVisible);
		public const string ShimmerHidden = nameof(ShimmerHidden);
	}

	// Dimensions below this are treated as "no value present yet" (empty bound TextBlock, source-less Image),
	// triggering the layout-slot fallback.
	private const double EmptySizeThreshold = 2;
	// Line width used when the layout grants an empty TextBlock no space at all (e.g. inside a horizontal StackPanel).
	private const double DefaultTextLineWidth = 100;
	// Approximates the height of a text line from FontSize for a TextBlock that measured empty.
	private const double TextLineHeightFactor = 1.4;

	public static DependencyProperty IsLoadingProperty { get; } = DependencyProperty.Register(
		nameof(IsLoading), typeof(bool), typeof(ShimmerView),
		new PropertyMetadata(true, (s, e) => ((ShimmerView)s).UpdateShimmerState()));

	/// <summary>Whether the placeholders are shown instead of the content. Default is true.</summary>
	public bool IsLoading
	{
		get => (bool)GetValue(IsLoadingProperty);
		set => SetValue(IsLoadingProperty, value);
	}

	public static DependencyProperty EnableShimmerProperty { get; } = DependencyProperty.Register(
		nameof(EnableShimmer), typeof(bool), typeof(ShimmerView),
		new PropertyMetadata(true, (s, e) => ((ShimmerView)s).OnEnableShimmerChanged()));

	/// <summary>Whether the shimmer plays while loading; when false, the placeholders are static. Default is true.</summary>
	public bool EnableShimmer
	{
		get => (bool)GetValue(EnableShimmerProperty);
		set => SetValue(EnableShimmerProperty, value);
	}

	public static DependencyProperty PlaceholderCornerRadiusProperty { get; } = DependencyProperty.Register(
		nameof(PlaceholderCornerRadius), typeof(CornerRadius), typeof(ShimmerView),
		new PropertyMetadata(default(CornerRadius), (s, e) => ((ShimmerView)s).OnPlaceholderAppearanceChanged()));

	/// <summary>The corner radius of rectangular placeholders.</summary>
	public CornerRadius PlaceholderCornerRadius
	{
		get => (CornerRadius)GetValue(PlaceholderCornerRadiusProperty);
		set => SetValue(PlaceholderCornerRadiusProperty, value);
	}

	private readonly record struct PlaceholderInfo(Rect Rect, bool IsCircle, FrameworkElement Element);

	// Original value of a property the shimmer overrides on content elements while loading
	// (e.g. Opacity of an element hidden behind its placeholder), restored once loaded.
	private readonly record struct PropertyValue(BindingBase? Binding, object LocalValue)
	{
		public static PropertyValue Capture(FrameworkElement element, DependencyProperty property) => new(
			element.GetBindingExpression(property)?.ParentBinding,
			element.ReadLocalValue(property));

		public void Restore(FrameworkElement element, DependencyProperty property)
		{
			if (Binding is { } binding)
			{
				element.SetBinding(property, binding);
			}
			else if (LocalValue == DependencyProperty.UnsetValue)
			{
				element.ClearValue(property);
			}
			else
			{
				element.SetValue(property, LocalValue);
			}
		}
	}

	private List<PlaceholderInfo> _lastPlaceholders = [];
	private readonly Dictionary<FrameworkElement, PropertyValue> _hiddenElements = [];
	// Per generation pass: the area of the overlay within which each visited element's descendants are visible.
	private readonly Dictionary<DependencyObject, Rect> _visibleAreas = [];
	private ContentPresenter? _contentPresenter;
	private Canvas? _overlay;
	private bool _isReady;
	private bool _isLayoutUpdatedHooked;

	public ShimmerView()
	{
		DefaultStyleKey = typeof(ShimmerView);

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	protected override void OnApplyTemplate()
	{
		if (_contentPresenter is { })
		{
			_contentPresenter.SizeChanged -= OnPartSizeChanged;
		}
		if (_overlay is { })
		{
			_overlay.SizeChanged -= OnPartSizeChanged;
		}

		// everything built on the previous template's overlay is gone with it: the next pass must rebuild from scratch
		StopShimmer();
		ClearOverlay();
		RemoveListStandIns();

		base.OnApplyTemplate();

		_contentPresenter = GetTemplateChild(TemplateParts.ContentPresenter) as ContentPresenter;
		_overlay = GetTemplateChild(TemplateParts.ShimmerOverlay) as Canvas;

		if (_contentPresenter is { })
		{
			_contentPresenter.SizeChanged += OnPartSizeChanged;
		}
		if (_overlay is { })
		{
			// the overlay is collapsed while not loading, so it (re)gains a size only once
			// the ShimmerVisible state shows it — that is the moment generation can succeed
			_overlay.SizeChanged += OnPartSizeChanged;
		}

		_isReady = true;
		UpdateShimmerState();
	}

	private void OnLoaded(object sender, RoutedEventArgs e) => UpdateShimmerState();

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		UnhookLayoutUpdated();

		// The content is left as it was found: an element reparented elsewhere must not stay hidden, and a list
		// must not keep a raised MinHeight. OnLoaded regenerates.
		StopShimmer();
		ClearOverlay();
		RestoreHiddenElements();
		RemoveListStandIns();
	}

	protected override void OnContentChanged(object oldContent, object newContent)
	{
		base.OnContentChanged(oldContent, newContent);
		OnContentReplaced();
	}

	protected override void OnContentTemplateChanged(DataTemplate oldContentTemplate, DataTemplate newContentTemplate)
	{
		base.OnContentTemplateChanged(oldContentTemplate, newContentTemplate);
		OnContentReplaced();
	}

	private void OnContentReplaced()
	{
		if (!_isReady) return;

		// The previous content's elements are no longer covered. A same-size replacement resizes nothing, so the
		// overlay is rebuilt from the new content on the layout pass that materializes it rather than right away.
		RestoreHiddenElements();
		RemoveListStandIns();
		StopShimmer();
		ClearOverlay();
		if (IsLoading)
		{
			HookLayoutUpdated();
		}
	}

	private void HookLayoutUpdated()
	{
		// LayoutUpdated is raised for every layout pass of the window: never listen while detached (e.g. a page kept
		// in a back stack whose IsLoading changes); OnLoaded regenerates, and hooks again if needed.
		if (_isLayoutUpdatedHooked || !IsLoaded) return;

		_isLayoutUpdatedHooked = true;
		LayoutUpdated += OnLayoutUpdated;
	}

	private void UnhookLayoutUpdated()
	{
		if (!_isLayoutUpdatedHooked) return;

		_isLayoutUpdatedHooked = false;
		LayoutUpdated -= OnLayoutUpdated;
	}

	private void OnLayoutUpdated(object? sender, object e)
	{
		if (IsLoading)
		{
			GenerateOverlay();
		}
		else
		{
			UnhookLayoutUpdated();
		}
	}

	private void OnEnableShimmerChanged()
	{
		if (EnableShimmer)
		{
			StartShimmerIfNeeded();
		}
		else
		{
			StopShimmer();
		}
	}

	private void OnPlaceholderAppearanceChanged()
	{
		// invalidate the cache so the next generation rebuilds with the new appearance
		_lastPlaceholders.Clear();
		if (_isReady && IsLoading)
		{
			GenerateOverlay();
		}
	}

	private void OnPartSizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (IsLoading)
		{
			GenerateOverlay();
		}
	}

	private void UpdateShimmerState()
	{
		if (!_isReady) return;

		if (IsLoading)
		{
			VisualStateManager.GoToState(this, VisualStateNames.ShimmerVisible, useTransitions: IsLoaded);
			GenerateOverlay();
		}
		else
		{
			VisualStateManager.GoToState(this, VisualStateNames.ShimmerHidden, useTransitions: IsLoaded);
			StopShimmer();
			ClearOverlay();
			RestoreHiddenElements();
			RemoveListStandIns();
			UnhookLayoutUpdated();
		}
	}

	private void GenerateOverlay()
	{
		if (_overlay is null || _contentPresenter is null) return;

		// Content changes without resizing the presenter or the overlay (rows arriving in a fixed-size list,
		// recycled containers, placeholder rows realizing): every layout pass while loading is a chance to catch
		// up, and SameGeometry keeps a pass where nothing moved down to a walk. Unhooked once loaded or unloaded.
		HookLayoutUpdated();

		// The memoized visible areas are this pass's: FillView reads them in UpdateListStandIns.
		_visibleAreas.Clear();

		// Before the size check: an empty list reserving the space of its placeholder rows can be
		// what gives the content its size.
		UpdateListStandIns(_contentPresenter);

		if (_overlay.ActualWidth < EmptySizeThreshold || _overlay.ActualHeight < EmptySizeThreshold)
		{
			// not laid out yet
			return;
		}

		var placeholders = new List<PlaceholderInfo>();
		CollectPlaceholders(_contentPresenter, placeholders);

		// Only the elements covered by a placeholder are hidden: ignored elements and container
		// backgrounds (e.g. a card's border) stay visible while loading.
		HideCoveredElements(placeholders);

		if (SameGeometry(placeholders, _lastPlaceholders))
		{
			// same geometry; just make sure the shimmer is running (e.g. first Loaded after OnApplyTemplate)
			StartShimmerIfNeeded();
			return;
		}

		StopShimmer();
		ClearOverlay();

		foreach (var placeholder in placeholders)
		{
			AddPlaceholder(placeholder, _overlay);
		}

		_lastPlaceholders = placeholders;
		StartShimmerIfNeeded();
	}

	private void CollectPlaceholders(DependencyObject parent, List<PlaceholderInfo> results)
	{
		foreach (var child in parent.GetChildren())
		{
			CollectPlaceholder(child, results);
		}
	}

	private void CollectPlaceholder(DependencyObject child, List<PlaceholderInfo> results)
	{
		if (child is not UIElement element || element.Visibility == Visibility.Collapsed)
		{
			return;
		}

		var fe = element as FrameworkElement;
		if (fe is { } && ShimmerView.GetIgnore(fe))
		{
			return;
		}

		var shape = fe is { } ? ShimmerView.GetShape(fe) : ShimmerShape.Auto;
		if (fe is { } && (shape != ShimmerShape.Auto || IsShimmerLeaf(element)))
		{
			if (TryCreatePlaceholderInfo(fe, shape, out var info))
			{
				results.Add(info);
			}
			return; // leaves are covered by a single placeholder; don't descend
		}

		if (element is ShimmerView)
		{
			// a nested shimmer view hides and restores its own content: walking into it would capture its hidden
			// state as the original, and mirror its leaves twice
			return;
		}

		// Only a container's content is mirrored, not its template chrome: e.g. a ListViewItem's full-size
		// background whose placeholder would cover the content's placeholders.
		if (element is ContentControl container && FindContentRoot(container) is { } contentRoot)
		{
			CollectPlaceholder(contentRoot, results);
			return;
		}

		if (fe is { } && _listStandIns.TryGetValue(fe, out var standIn))
		{
			CollectStandInPlaceholders(fe, standIn, results);
			return;
		}

		CollectPlaceholders(child, results);
	}

	private static UIElement? FindContentRoot(ContentControl container)
	{
		// ContentTemplateRoot isn't reliably available (e.g. list item containers on their first pass), and
		// direct UIElement content is its own root; otherwise fall back to the template's ContentPresenter.
		if ((container.ContentTemplateRoot ?? container.Content as UIElement) is { } root)
		{
			// Jumping over the chrome must not jump over collapsed chrome (e.g. a collapsed Expander's content),
			// nor land on content that isn't under the container yet.
			return IsVisibleDescendant(root, container) ? root : null;
		}

		var pending = new Queue<DependencyObject>(container.GetChildren());
		while (pending.Count > 0)
		{
			var current = pending.Dequeue();
			if (current is UIElement { Visibility: Visibility.Collapsed })
			{
				continue;
			}
			if (current is ContentPresenter presenter)
			{
				return presenter;
			}
			if (current is Control)
			{
				continue; // a nested control's presenter belongs to that control
			}
			foreach (var child in current.GetChildren())
			{
				pending.Enqueue(child);
			}
		}

		return null;
	}

	private static bool IsVisibleDescendant(UIElement element, DependencyObject ancestor)
	{
		for (var current = VisualTreeHelper.GetParent(element); current is { }; current = VisualTreeHelper.GetParent(current))
		{
			if (ReferenceEquals(current, ancestor)) return true;
			if (current is UIElement { Visibility: Visibility.Collapsed }) return false;
		}

		return false;
	}

	private static bool IsShimmerLeaf(UIElement element)
		=> element is TextBlock or Image or Shape or ButtonBase || IsInputControl(element);

	// Controls whose value is their own chrome: one placeholder covers the whole control. RangeBase also matches
	// ScrollBar, which only ever appears in a ScrollViewer's chrome, and chrome is never walked.
	private static bool IsInputControl(UIElement element)
		=> element is RangeBase or ProgressRing or ToggleSwitch
			or TextBox or PasswordBox or RichEditBox or AutoSuggestBox or ComboBox
			or DatePicker or TimePicker or CalendarDatePicker or CalendarView
			or NumberBox or RatingControl or ColorPicker;

	// Same placeholders, within layout rounding: exact comparison would rebuild the overlay (and restart the
	// shimmer) on sub-pixel drift between passes.
	private static bool SameGeometry(List<PlaceholderInfo> current, List<PlaceholderInfo> previous)
	{
		if (current.Count != previous.Count) return false;

		for (var i = 0; i < current.Count; i++)
		{
			var a = current[i];
			var b = previous[i];
			if (!ReferenceEquals(a.Element, b.Element) || a.IsCircle != b.IsCircle || !IsClose(a.Rect, b.Rect)) return false;
		}

		return true;
	}

	private bool TryCreatePlaceholderInfo(FrameworkElement element, ShimmerShape shape, out PlaceholderInfo info)
	{
		info = default;
		if (_overlay is null) return false;

		var bounds = element.TransformToVisual(_overlay)
			.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

		if (bounds.Width < EmptySizeThreshold || bounds.Height < EmptySizeThreshold)
		{
			// The element has no value yet, so its own size is no use; fall back to the space the
			// layout granted it (its slot), synthesizing a text-line size where the slot gives no hint.
			if (!TryGetLayoutSlotBounds(element, out var slotBounds)) return false;

			if (bounds.Width < EmptySizeThreshold)
			{
				bounds.X = slotBounds.X;
				// An element not stretched across its slot doesn't fill it: its least width, if it has one, is its size
				bounds.Width = element.HorizontalAlignment != HorizontalAlignment.Stretch && element.MinWidth >= EmptySizeThreshold
					? element.MinWidth
					: slotBounds.Width >= EmptySizeThreshold
					? slotBounds.Width
					: element is TextBlock ? DefaultTextLineWidth : 0;
			}
			if (bounds.Height < EmptySizeThreshold)
			{
				bounds.Y = slotBounds.Y;
				bounds.Height = element is TextBlock textBlock
					? textBlock.FontSize * TextLineHeightFactor
					: slotBounds.Height;
			}

			if (bounds.Width < EmptySizeThreshold || bounds.Height < EmptySizeThreshold) return false;
		}

		var isCircle = shape == ShimmerShape.Circle || (shape == ShimmerShape.Auto && element is Ellipse);
		if (isCircle)
		{
			var diameter = Math.Min(bounds.Width, bounds.Height);
			bounds = new Rect(
				bounds.X + ((bounds.Width - diameter) / 2),
				bounds.Y + ((bounds.Height - diameter) / 2),
				diameter,
				diameter);
		}

		// Only what the user can see is mirrored: a row scrolled half out of its list gets half a placeholder, a
		// row realized ahead of the viewport gets none (and a clipped circle is no longer one).
		var visible = bounds;
		visible.Intersect(GetVisibleArea(element));
		if (visible.IsEmpty || visible.Width < EmptySizeThreshold || visible.Height < EmptySizeThreshold) return false;

		info = new PlaceholderInfo(visible, isCircle && visible == bounds, element);
		return true;
	}

	// The area of the overlay within which the element (and its descendants) can be seen: the overlay's bounds,
	// narrowed by every scrolling viewport and explicit Clip on the way up to the content presenter (for content)
	// or the overlay (for stand-in rows). Memoized per pass. Empty when the element is not under either.
	private Rect GetVisibleArea(DependencyObject element)
	{
		if (_visibleAreas.TryGetValue(element, out var area)) return area;

		if (_overlay is null)
		{
			area = Rect.Empty;
		}
		else if (ReferenceEquals(element, _overlay) || ReferenceEquals(element, _contentPresenter))
		{
			area = new Rect(0, 0, _overlay.ActualWidth, _overlay.ActualHeight);
		}
		else if (VisualTreeHelper.GetParent(element) is not { } parent)
		{
			area = Rect.Empty;
		}
		else
		{
			area = GetVisibleArea(parent);
			if (!area.IsEmpty && element is UIElement ui && GetOwnClip(ui) is { } clip)
			{
				area.Intersect(ui.TransformToVisual(_overlay).TransformBounds(clip));
			}
		}

		_visibleAreas[element] = area;
		return area;
	}

	private static Rect? GetOwnClip(UIElement element) => element switch
	{
		{ Clip: RectangleGeometry geometry } => geometry.Rect,
		ScrollContentPresenter viewport => new Rect(0, 0, viewport.ActualWidth, viewport.ActualHeight),
		ScrollPresenter viewport => new Rect(0, 0, viewport.ActualWidth, viewport.ActualHeight), // ScrollView's viewport, which Uno doesn't clip to
		_ => null,
	};

	// The space the layout granted the element (its margin excluded), in overlay coordinates.
	private bool TryGetLayoutSlotBounds(FrameworkElement element, out Rect bounds)
	{
		bounds = default;
		if (_overlay is null || VisualTreeHelper.GetParent(element) is not UIElement parent) return false;

		var slot = LayoutInformation.GetLayoutSlot(element);
		var margin = element.Margin;
		slot = new Rect(
			slot.X + margin.Left,
			slot.Y + margin.Top,
			Math.Max(0, slot.Width - margin.Left - margin.Right),
			Math.Max(0, slot.Height - margin.Top - margin.Bottom));
		bounds = parent.TransformToVisual(_overlay).TransformBounds(slot);
		return true;
	}

	private void AddPlaceholder(PlaceholderInfo info, Canvas overlay)
	{
		// The toolkit's Shimmer rather than Uno's own placeholder and sweeping band: the app's shimmers look like this
		var rect = info.Rect;
		var placeholder = new Shimmer
		{
			Width = rect.Width,
			Height = rect.Height,
			CornerRadius = info.IsCircle ? new CornerRadius(rect.Width / 2) : OwnCornerRadius(info.Element) ?? PlaceholderCornerRadius,
			IsActive = false, // started by StartShimmerIfNeeded, with the rest
		};

		Canvas.SetLeft(placeholder, rect.X);
		Canvas.SetTop(placeholder, rect.Y);
		overlay.Children.Add(placeholder);
	}

	// A rounded element's own corners (a card, a button), so its placeholder has its shape.
	private static CornerRadius? OwnCornerRadius(FrameworkElement element) => element switch
	{
		Control { CornerRadius: var radius } when radius != default => radius,
		Grid { CornerRadius: var radius } when radius != default => radius,
		Border { CornerRadius: var radius } when radius != default => radius,
		_ => null,
	};

	private void HideCoveredElements(List<PlaceholderInfo> placeholders)
	{
		var covered = new HashSet<FrameworkElement>();
		foreach (var placeholder in placeholders)
		{
			covered.Add(placeholder.Element);
		}

		// restore elements no longer covered (e.g. content replaced or re-laid out while loading)
		foreach (var element in _hiddenElements.Keys.Where(x => !covered.Contains(x)).ToList())
		{
			RestoreElement(element);
		}

		foreach (var element in covered)
		{
			if (_hiddenElements.ContainsKey(element)) continue;

			_hiddenElements[element] = PropertyValue.Capture(element, UIElement.OpacityProperty);
			element.Opacity = 0;
		}
	}

	private void RestoreHiddenElements()
	{
		foreach (var (element, original) in _hiddenElements)
		{
			original.Restore(element, UIElement.OpacityProperty);
		}
		_hiddenElements.Clear();
	}

	private void RestoreElement(FrameworkElement element)
	{
		if (_hiddenElements.Remove(element, out var original))
		{
			original.Restore(element, UIElement.OpacityProperty);
		}
	}

	private void StartShimmerIfNeeded() => SetShimmersActive(EnableShimmer && IsLoading && IsLoaded);

	private void StopShimmer() => SetShimmersActive(false);

	private void SetShimmersActive(bool isActive)
	{
		foreach (var shimmer in _overlay?.Children.OfType<Shimmer>() ?? [])
		{
			if (shimmer.IsActive != isActive)
			{
				shimmer.IsActive = isActive;
			}
		}
	}

	private void ClearOverlay()
	{
		_lastPlaceholders.Clear();
		_visibleAreas.Clear();
		if (_overlay is { } overlay)
		{
			// the stand-in host stays: recreating it would re-realize its placeholder rows on every pass
			for (var i = overlay.Children.Count - 1; i >= 0; i--)
			{
				if (!ReferenceEquals(overlay.Children[i], _standInHost))
				{
					overlay.Children.RemoveAt(i);
				}
			}
		}
	}
}
