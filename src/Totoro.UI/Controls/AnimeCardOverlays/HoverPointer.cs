using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// While a child (e.g. a button) captures the pointer, leaving the card raises no PointerExited; releasing outside
/// only raises PointerCaptureLost. Hover behaviors use this to still notice the pointer has left.
/// </summary>
internal static class HoverPointer
{
	public static void AddCaptureLostHandler(UIElement element, PointerEventHandler handler) =>
		element.AddHandler(UIElement.PointerCaptureLostEvent, handler, handledEventsToo: true); // buttons mark it handled

	public static void RemoveCaptureLostHandler(UIElement element, PointerEventHandler handler) =>
		element.RemoveHandler(UIElement.PointerCaptureLostEvent, handler);

	public static bool IsOutside(object sender, PointerRoutedEventArgs e)
	{
		if (sender is not FrameworkElement element)
		{
			return false;
		}

		var p = e.GetCurrentPoint(element).Position;
		return p.X < 0 || p.Y < 0 || p.X > element.ActualWidth || p.Y > element.ActualHeight;
	}
}
