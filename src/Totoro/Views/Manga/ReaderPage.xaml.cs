using System.Numerics;
using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Totoro.UI.Navigation;
using Totoro.ViewModels.Manga;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Totoro.Views.Manga;

/// <summary>
/// The manga reader: a page in the main window (not a window of its own).
/// Left and right turn pages (following the reading direction), as do Page Up/Down and Space; Escape goes back.
/// Pages turn over on the spine as a book's do (see <see cref="Turn"/>).
/// </summary>
public sealed partial class ReaderPage : Page
{
	// The spread showing, to tell which way a turn went.
	private int _shownSpread = -1;

	public ReaderPage()
	{
		InitializeComponent();
		Loaded += (_, _) =>
		{
			Focus(FocusState.Programmatic);
		};
		Unloaded += (_, _) => TurnLayer.Children.Clear();
	}

	[GeneratedDependencyProperty]
	public partial ReaderViewModel? ViewModel { get; set; }

	// Before the controls see them: the flip view and the page slider take the arrow keys for themselves otherwise.
	private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (ViewModel is not { } vm || e.OriginalSource is TextBox)
		{
			return;
		}

		// Left goes forward when reading right to left.
		var forward = vm.IsRightToLeft ? VirtualKey.Left : VirtualKey.Right;
		var back = vm.IsRightToLeft ? VirtualKey.Right : VirtualKey.Left;
		switch (e.Key)
		{
			case VirtualKey.Escape:
				if (Navigators.TryGetNavigator("main", out var main))
				{
					_ = main.GoBackAsync();
				}

				break;
			case var key when key == forward || key is VirtualKey.Space or VirtualKey.PageDown:
				vm.NextPageCommand.Execute(null);
				break;
			case var key when key == back || key is VirtualKey.PageUp:
				vm.PreviousPageCommand.Execute(null);
				break;
			default:
				return;
		}

		e.Handled = true;
	}

	private void OnSpreadChanged(object sender, SelectionChangedEventArgs e)
	{
		var index = PagedView.SelectedIndex;
		var spreads = PagedView.ItemsSource;
		// New spreads (another chapter, two pages turned on or off) aren't a turn: the flip view selects as it takes them,
		// and asking it for an item's container meanwhile crashes the app.
		var turned = ReferenceEquals(spreads, _shownSpreads) && _shownSpread >= 0 && index >= 0 && Math.Abs(index - _shownSpread) == 1;
		var from = _shownSpread;
		_shownSpread = index;
		_shownSpreads = spreads;
		// A turn still going ends where it is; a new one starts from what shows now.
		TurnLayer.Children.Clear();
		// Not on opening a chapter or jumping (the slider), nor where the user turned animations off.
		if (turned && new UISettings().AnimationsEnabled && ViewModel is { } vm)
		{
			// Now, before the new spread has shown a frame on its own: the copies cover it until they've turned.
			Turn(from, index, vm.IsRightToLeft);
		}
	}

	// The spreads the flip view showed at the last selection, to tell a turn from new spreads.
	private object? _shownSpreads;

	// How long half a leaf's turn takes (it lifts, then lands); a single page turns away (or back) in twice that.
	private static readonly TimeSpan s_halfTurn = TimeSpan.FromMilliseconds(260);

	// How dark a page is in the shadow of one turning over it, at most.
	private const double ShadowOpacity = 0.45;

	// The rotation that turns a page a quarter of the way over, toward the reader, for one lying right of its spine.
	private const float QuarterTurn = -90;

	/// <summary>
	/// Turns the page over from spread <paramref name="from"/> (what showed) to <paramref name="to"/> (what the flip view
	/// shows now), as a book's: in copies of the pages over the flip view, which go once they've turned.
	/// <para>
	/// Two pages to two: the leaf lifts on the spine showing the page left behind, and lands on the other side showing
	/// the next, its back, over the page that was there; the pages it uncovers and covers are in its shadow.
	/// </para>
	/// <para>
	/// A wide page (two facing pages drawn as one) is an open book itself: its halves are two pages, so to or from two
	/// pages, or another wide page, a leaf turns over the same way.
	/// </para>
	/// <para>
	/// Otherwise (one page at a time, or the first and last pages, which are alone): the page turns over its spine the
	/// same way, uncovering the next beneath, and its back (blank paper) lands on the other side and fades; going back,
	/// that paper turns back in as the page before. Between a wide page and a lone one there's no page to turn as a
	/// book's: they cross-fade.
	/// </para>
	/// </summary>
	private void Turn(int from, int to, bool rightToLeft)
	{
		if (PagedView.ContainerFromIndex(from) is not FrameworkElement before || PagedView.ContainerFromIndex(to) is not FrameworkElement after ||
			ShownPages(before) is not { Count: > 0 } old || ShownPages(after) is not { Count: > 0 } next)
		{
			return;
		}

		var forward = to > from;
		var compositor = ElementCompositionPreview.GetElementVisual(TurnLayer).Compositor;
		var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
		if (Leaves(old) is [var oldLeft, var oldRight] && Leaves(next) is [var nextLeft, var nextRight])
		{
			SeeFromSpine((oldLeft.Shown.Right + oldRight.Shown.Left) / 2);
			// The leaf goes right to left reading on left to right, or back reading right to left.
			var leftward = forward != rightToLeft;
			var (lifts, stays, lands, uncovered) = leftward ? (oldRight, oldLeft, nextLeft, nextRight) : (oldLeft, oldRight, nextRight, nextLeft);
			CastShadow(uncovered, ShadowOpacity, 0, TimeSpan.Zero);
			Copy(stays);
			CastShadow(stays, 0, ShadowOpacity, s_halfTurn);
			// Its back: edge-on (so unseen) until the leaf is upright.
			Rotate(Copy(lands), hingeLeft: !leftward, leftward ? -QuarterTurn : QuarterTurn, 0, s_halfTurn, s_halfTurn, landing: true);
			Rotate(Copy(lifts), hingeLeft: leftward, 0, leftward ? QuarterTurn : -QuarterTurn, TimeSpan.Zero, s_halfTurn, landing: false);
		}
		else if (IsWide(old) || IsWide(next))
		{
			Fade(Copy(old));
		}
		else
		{
			// A leaf turn too: the page's back is blank paper, on the other side of its spine (left of a page read left to
			// right), which fades away there going on, and fades in to turn back over going back.
			var hingeLeft = !rightToLeft;
			var page = Bounds(forward ? old : next);
			var spine = hingeLeft ? page.Left : page.Right;
			SeeFromSpine(spine);
			var back = Paper(new Rect(hingeLeft ? spine - page.Width : spine, page.Y, page.Width, page.Height));
			if (forward)
			{
				CastShadow(Bounds(next), ShadowOpacity, 0, TimeSpan.Zero);
				Rotate(Copy(old), hingeLeft, 0, hingeLeft ? QuarterTurn : -QuarterTurn, TimeSpan.Zero, s_halfTurn, landing: false);
				Rotate(back, !hingeLeft, hingeLeft ? -QuarterTurn : QuarterTurn, 0, s_halfTurn, s_halfTurn, landing: true);
				// Paper while it turns; gone once it's down.
				Fade(back.Children[0], 1, 0, s_halfTurn * 1.6, s_halfTurn * 0.4);
			}
			else
			{
				Copy(old);
				CastShadow(Bounds(old), 0, ShadowOpacity, s_halfTurn);
				Fade(back.Children[0], 0, 1, TimeSpan.Zero, s_halfTurn * 0.4);
				Rotate(back, !hingeLeft, 0, hingeLeft ? -QuarterTurn : QuarterTurn, TimeSpan.Zero, s_halfTurn, landing: false);
				Rotate(Copy(next), hingeLeft, hingeLeft ? QuarterTurn : -QuarterTurn, 0, s_halfTurn, s_halfTurn, landing: true);
			}
		}

		batch.End();
		// Unless another turn has replaced them meanwhile.
		var turn = TurnLayer.Children.ToList();
		batch.Completed += (_, _) => DispatcherQueue.TryEnqueue(() =>
		{
			if (turn.Count > 0 && TurnLayer.Children.Contains(turn[0]))
			{
				TurnLayer.Children.Clear();
			}
		});
	}

	/// <summary>
	/// Puts the turn layer in perspective, seen from in front of the spine at <paramref name="spineX"/>: what turns in it
	/// comes toward the reader as it lifts. On the layer, not each page: a visual is flattened after its own transform,
	/// so a page's own perspective would be lost.
	/// </summary>
	private void SeeFromSpine(double spineX)
	{
		// Far enough that a lifted page doesn't come out over the bar much.
		const float Depth = 2500;
		var (x, y) = ((float)spineX, (float)TurnLayer.ActualHeight / 2);
		ElementCompositionPreview.GetElementVisual(TurnLayer).TransformMatrix =
			Matrix4x4.CreateTranslation(-x, -y, 0)
			* new Matrix4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, -1 / Depth, 0, 0, 0, 1)
			* Matrix4x4.CreateTranslation(x, y, 0);
	}

	/// <summary>
	/// A page as it shows, or the part of it that does: its image, where the whole image is in the reader (its
	/// container's place), and where the part is (all of it, but for a half of a wide page).
	/// </summary>
	private sealed record Piece(ImageSource Image, Rect Bounds, Rect Shown);

	/// <summary>Whether a spread is a wide page: wider than tall, as two facing pages drawn as one are.</summary>
	private static bool IsWide(List<Piece> pages) => pages is [{ Bounds: var bounds }] && bounds.Width > bounds.Height;

	/// <summary>A spread's two leaves, left and right: its two pages, or a wide page's halves; one page has none.</summary>
	private static List<Piece> Leaves(List<Piece> pages)
	{
		if (!IsWide(pages))
		{
			return pages.Count == 2 ? pages : [];
		}

		var (image, bounds, _) = pages[0];
		var half = bounds.Width / 2;
		return
		[
			new(image, bounds, new Rect(bounds.X, bounds.Y, half, bounds.Height)),
			new(image, bounds, new Rect(bounds.X + half, bounds.Y, half, bounds.Height)),
		];
	}

	/// <summary>A spread's pages as they show, left to right: each image and where it is in the reader (its container's place).</summary>
	private static List<Piece> ShownPages(FrameworkElement container)
	{
		// The pages' scroll view reads left to right, whatever the flip view's direction, and fills the container.
		if (container.FindDescendant<ScrollView>() is not { } view)
		{
			return [];
		}

		return
		[
			.. container.FindDescendants().OfType<Image>()
				.Where(x => x.Source is not null && x.ActualWidth > 0 && x.FindAscendant<ContentControl>() is { Visibility: Visibility.Visible })
				.Select(x => (x.Source, Bounds: x.TransformToVisual(view).TransformBounds(new Rect(0, 0, x.ActualWidth, x.ActualHeight))))
				.Select(x => new Piece(x.Source, x.Bounds, x.Bounds))
				.OrderBy(x => x.Bounds.X)
		];
	}

	private static Rect Bounds(List<Piece> pages)
	{
		var bounds = pages[0].Shown;
		foreach (var page in pages.Skip(1))
		{
			bounds.Union(page.Shown);
		}

		return bounds;
	}

	/// <summary>Copies a page (or a spread's pages, which then turn together) onto the turn layer, where it shows.</summary>
	private Canvas Copy(Piece page) => Copy([page]);

	private Canvas Copy(List<Piece> pages)
	{
		var bounds = Bounds(pages);
		// Only what shows of each image: half of a wide page is a page of its own.
		var copy = new Canvas { Width = bounds.Width, Height = bounds.Height, Clip = new RectangleGeometry { Rect = new Rect(0, 0, bounds.Width, bounds.Height) } };
		foreach (var (image, place, _) in pages)
		{
			var element = new Image { Source = image, Width = place.Width, Height = place.Height, Stretch = Stretch.Fill };
			Canvas.SetLeft(element, place.X - bounds.X);
			Canvas.SetTop(element, place.Y - bounds.Y);
			copy.Children.Add(element);
		}

		Canvas.SetLeft(copy, bounds.X);
		Canvas.SetTop(copy, bounds.Y);
		TurnLayer.Children.Add(copy);
		return copy;
	}

	/// <summary>A lone page's back, blank paper, on the turn layer at <paramref name="bounds"/>.</summary>
	private Canvas Paper(Rect bounds)
	{
		// The sheet is a child, so it can fade while Rotate shows and hides the whole.
		var paper = new Canvas { Width = bounds.Width, Height = bounds.Height };
		paper.Children.Add(new Rectangle { Width = bounds.Width, Height = bounds.Height, Fill = new SolidColorBrush(Colors.WhiteSmoke) });
		Canvas.SetLeft(paper, bounds.X);
		Canvas.SetTop(paper, bounds.Y);
		TurnLayer.Children.Add(paper);
		return paper;
	}

	/// <summary>Fades a copy out over a turn's time, uncovering what the flip view shows now.</summary>
	private static void Fade(UIElement copy) => Fade(copy, 1, 0, TimeSpan.Zero, s_halfTurn * 2);

	private static void Fade(UIElement element, float from, float to, TimeSpan delay, TimeSpan duration)
	{
		var visual = ElementCompositionPreview.GetElementVisual(element);
		visual.Opacity = from;
		var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
		fade.InsertKeyFrame(0, from);
		fade.InsertKeyFrame(1, to);
		fade.Duration = duration;
		fade.DelayTime = delay;
		fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
		visual.StartAnimation(nameof(visual.Opacity), fade);
	}

	/// <summary>The shadow a turning page casts on a page, darkening or lightening over a half turn (or more).</summary>
	private void CastShadow(Piece page, double from, double to, TimeSpan delay) => CastShadow(page.Shown, from, to, delay);

	private void CastShadow(Rect bounds, double from, double to, TimeSpan delay, int halves = 1)
	{
		var shadow = new Rectangle { Width = bounds.Width, Height = bounds.Height, Fill = new SolidColorBrush(Colors.Black) };
		Canvas.SetLeft(shadow, bounds.X);
		Canvas.SetTop(shadow, bounds.Y);
		TurnLayer.Children.Add(shadow);

		var visual = ElementCompositionPreview.GetElementVisual(shadow);
		visual.Opacity = (float)from;
		var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
		fade.InsertKeyFrame(0, (float)from);
		fade.InsertKeyFrame(1, (float)to);
		fade.Duration = s_halfTurn * halves;
		fade.DelayTime = delay;
		fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
		visual.StartAnimation(nameof(visual.Opacity), fade);
	}

	/// <summary>
	/// Turns a page copy on its spine (its left or right edge): from <paramref name="from"/> to <paramref name="to"/>
	/// degrees, speeding up as it lifts and slowing as it lands (in the layer's perspective, see <see cref="SeeFromSpine"/>).
	/// </summary>
	private static void Rotate(FrameworkElement page, bool hingeLeft, float from, float to, TimeSpan delay, TimeSpan duration, bool landing)
	{
		var visual = ElementCompositionPreview.GetElementVisual(page);
		var compositor = visual.Compositor;
		var (width, height) = ((float)page.Width, (float)page.Height);
		visual.CenterPoint = new Vector3(hingeLeft ? 0 : width, height / 2, 0);
		visual.RotationAxis = Vector3.UnitY;
		visual.RotationAngleInDegrees = from;

		var turn = compositor.CreateScalarKeyFrameAnimation();
		turn.InsertKeyFrame(0, from);
		turn.InsertKeyFrame(1, to, landing
			? compositor.CreateCubicBezierEasingFunction(new(0.2f, 0.6f), new(0.35f, 1f))
			: compositor.CreateCubicBezierEasingFunction(new(0.55f, 0f), new(0.8f, 0.4f)));
		turn.Duration = duration;
		turn.DelayTime = delay;
		turn.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
		visual.StartAnimation(nameof(visual.RotationAngleInDegrees), turn);

		// Edge-on, a page would show as a line down the spine (perspective draws it so): unseen while it waits to land,
		// and once it has lifted all the way.
		var hidden = compositor.CreateScalarKeyFrameAnimation();
		var step = compositor.CreateStepEasingFunction();
		hidden.InsertKeyFrame(0, landing ? 0 : 1);
		hidden.InsertKeyFrame(1, landing ? 1 : 0, step);
		hidden.Duration = landing ? TimeSpan.FromMilliseconds(1) : duration;
		hidden.DelayTime = delay;
		hidden.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
		visual.StartAnimation(nameof(visual.Opacity), hidden);
	}

	private void OnRetryClick(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { Tag: ReaderPageItem page })
		{
			ViewModel?.RetryPageCommand.Execute(page);
		}
	}

	public static string Where(string chapter, string page) => string.IsNullOrEmpty(page) ? chapter : $"{chapter} · {page}";

	public static string FailedText(int number) => $"Page {number} didn't load.";

	// For a spread's pages: one spans both columns, centred; two meet in the middle.

	public static int LeftSpan(bool single) => single ? 2 : 1;

	public static HorizontalAlignment LeftAlignment(bool single) => single ? HorizontalAlignment.Center : HorizontalAlignment.Right;

	public static Visibility VisibleIfTwo(bool single) => single ? Visibility.Collapsed : Visibility.Visible;

	public static FlowDirection Direction(bool rightToLeft) => rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
