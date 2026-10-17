using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>When an overlay is on its card.</summary>
public enum OverlayMode
{
	/// <summary>While the pointer is over the card (e.g. its actions).</summary>
	OnHover,

	/// <summary>For as long as it's attached (e.g. a badge).</summary>
	Always,
}

/// <summary>
/// Something shown over an <see cref="AnimeCard"/>, in its OverlayLayer: attached to the cards of a page (in its card
/// template's Interaction.Behaviors), so the card itself stays plain and each page adds what it needs. Behaviors that
/// show nothing of their own (like <see cref="HoverEffectsBehavior"/>) aren't overlays.
/// </summary>
public abstract class AnimeCardOverlay<TControl> : BehaviorBase<AnimeCard>
	where TControl : UIElement
{
	private TControl? Control { get; set; }

	/// <summary>When it's on the card; on hover unless the overlay says otherwise.</summary>
	protected virtual OverlayMode Mode => OverlayMode.OnHover;

	/// <summary>The card's layer for overlays, which covers it.</summary>
	protected Grid? Layer => AssociatedObject?.FindName("OverlayLayer") as Grid;

	protected override void OnAttached()
	{
		if (Layer is not { } layer)
		{
			return;
		}

		if (Mode == OverlayMode.Always)
		{
			Show(layer);
			return;
		}

		layer.PointerEntered += OnPointerEntered;
		layer.PointerExited += OnPointerExited;
	}

	protected override void OnDetaching()
	{
		if (Layer is not { } layer)
		{
			return;
		}

		layer.PointerEntered -= OnPointerEntered;
		layer.PointerExited -= OnPointerExited;
		Hide(layer);
	}

	private void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (sender is Grid layer)
		{
			Show(layer);
			OnPointerEntered();
		}
	}

	private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (sender is Grid layer)
		{
			Hide(layer);
			OnPointerExited();
		}
	}

	private void Show(Grid layer)
	{
		Control = CreateOverlay();
		layer.Children.Add(Control);
	}

	private void Hide(Grid layer)
	{
		if (Control is null)
		{
			return;
		}

		OnHiding(Control);
		Control.Visibility = Visibility.Collapsed;
		layer.Children.Remove(Control);
		Control = null;
	}

	protected abstract TControl CreateOverlay();

	/// <summary>Called right before the overlay is collapsed and removed, i.e. before its hide animation runs.</summary>
	protected virtual void OnHiding(TControl overlay) { }

	protected virtual void OnPointerEntered() { }

	protected virtual void OnPointerExited() { }
}
