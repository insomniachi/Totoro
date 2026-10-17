using System.Reflection;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using DevWinUI;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
// DevWinUI has its own FindDescendant; prefer the Community Toolkit one.
using VisualTree = CommunityToolkit.WinUI.DependencyObjectExtensions;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// A gooey "more" button shown on hover that fans out into one sub-button per entry in <see cref="Actions"/>.
/// Declare the actions inline, or as a shared <see cref="CardActionCollection"/> resource.
/// </summary>
public partial class ActionsOverlay : AnimeCardOverlay<Viewbox>
{
	public ActionsOverlay()
	{
		// Non-null by default so actions can be declared inline with property element syntax.
		Actions = new CardActionCollection();
	}

	[GeneratedDependencyProperty]
	public partial IList<CardAction>? Actions { get; set; }

	protected override Viewbox CreateOverlay()
	{
		var control = new GooeyButton()
		{
			Content = CreateIcon(Symbol.More, 32),
		};

		// Colours come from theme resources (see ActionsOverlayResources) rather than being set here,
		// so they follow the card's own theme and update when it changes.
		control.Resources.MergedDictionaries.Add(new ActionsOverlayResources());

		control.ItemInvoked += OnInvoked;
		control.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => ClampGooeyEffect(control)), handledEventsToo: true);
		control.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => ClampGooeyEffect(control)), handledEventsToo: true);
		control.Loaded += (_, _) => StyleTemplate(control);
		control.RegisterPropertyChangedCallback(GooeyButton.ExpandedProperty, (_, _) => FadeItemOverlays(control));
		foreach (var action in Actions ?? [])
		{
			control.Items.Add(CreateItem(action));
		}

		var vb = new Viewbox()
		{
			Child = control,
			Height = 45,
			Width = 45,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			Margin = new Thickness(8),
		};

		Implicit.SetShowAnimations(vb,
		[
			new TranslationAnimation() 
			{
				Duration = TimeSpan.FromMilliseconds(500),
				From = "0, -10, 0",
				To = "0" 
			}
		]);
		Implicit.SetHideAnimations(vb,
		[
			new OpacityAnimation()
			{
				Duration = TimeSpan.FromMilliseconds(500),
				From = 1,
				To = 0
			},
			new ScalarAnimation
			{
				Target = "Translation.Y",
				Duration = TimeSpan.FromMilliseconds(500),
				To = -10,
				KeyFrames =
				[
					new ScalarKeyFrame { Key = 0.1, Value = 30 },
					new ScalarKeyFrame { Key = 0.5, Value = 0 }
				]
			}
		]);

		return vb;
	}

	private void OnInvoked(object sender, GooeyButton.GooeyButtonItemInvokedEventArgs args)
	{
		// GooeyButton reports the invoked item's Content, which is the icon CreateItem tagged with its action.
		if (args.Item is not FrameworkElement { Tag: CardAction action })
		{
			return;
		}

		var parameter = action.CommandParameter ?? AssociatedObject?.Anime;
		if (action.Command?.CanExecute(parameter) == true)
		{
			action.Command.Execute(parameter);
		}

		// GooeyButton stays open after an item is invoked; close it like a menu would.
		if (sender is GooeyButton button)
		{
			button.Expanded = false;
		}
	}

	protected override void OnHiding(Viewbox overlay)
	{
		// Collapsed items are parked under the main circle, and XAML fades each element on its own rather than as a
		// flattened layer, so they would show through while the button fades out. That covers both items whose
		// collapse animation hasn't finished yet and their Win2D blobs, which are still drawn when collapsed
		// (setting Opacity hides those too). Expanded items sit clear of the main circle, so leave them to fade.
		if (overlay.Child is not GooeyButton { Expanded: false } button)
		{
			return;
		}

		foreach (var item in button.Items.OfType<GooeyButtonItem>())
		{
			item.Opacity = 0;
			item.Visibility = Visibility.Collapsed;
		}
	}

	private static GooeyButtonItem CreateItem(CardAction action)
	{
		// Scale every kind of icon (symbol, glyph, path, bitmap...) into the same box: the whole button is shrunk by its
		// Viewbox (80px -> 45px), so icons need to be drawn this big to stay legible.
		var icon = new Viewbox
		{
			Width = ItemIconSize,
			Height = ItemIconSize,
			Child = action.Icon?.CreateIconElement(),
			Tag = action,
		};

		var item = new GooeyButtonItem()
		{
			Content = icon,
			BorderBrush = s_borderBrush,
			BorderThickness = new Thickness(BorderWidth),
		};

		if (!string.IsNullOrEmpty(action.Label))
		{
			ToolTipService.SetToolTip(item, action.Label);
			AutomationProperties.SetName(item, action.Label);
		}

		// The item's circle is drawn by Win2D, which has no stroke. Its template root is a Grid bound to
		// BorderBrush/BorderThickness, so round that Grid into a circle to draw the ring on top.
		item.Loaded += (_, _) =>
		{
			if (VisualTree.FindDescendant<Grid>(item, x => x.Name == "LayoutRoot") is { } root)
			{
				root.CornerRadius = new CornerRadius(Math.Min(item.ActualWidth, item.ActualHeight) / 2);
			}
		};

		return item;
	}

	private const double ItemIconSize = 28;

	// The overlay is scaled down by its Viewbox (80px -> 45px), so this ends up roughly 1px on screen.
	private const double BorderWidth = 1.8;
	private static readonly SolidColorBrush s_borderBrush = new(Colors.Black);

	private static readonly TimeSpan s_overlayFadeDuration = TimeSpan.FromMilliseconds(150);

	// The main circle's Ellipse no longer has a fill (see StyleTemplate), so items sliding back under it on collapse
	// would show their ring and icon through it. Fade those out as the collapse starts, and back in on expand.
	// Only the item's template root is faded; the item's own Opacity also drives its Win2D blob, which should keep
	// merging back into the main circle as before.
	private void FadeItemOverlays(GooeyButton button)
	{
		var opacity = button.Expanded ? 1 : 0;

		foreach (var item in button.Items.OfType<GooeyButtonItem>())
		{
			if (VisualTree.FindDescendant<Grid>(item, x => x.Name == "LayoutRoot") is { } root)
			{
				AnimationBuilder.Create()
					.Opacity(to: opacity, duration: s_overlayFadeDuration)
					.Start(root);
			}
		}
	}

	private const double FillOpacity = 0.9;

	// The main circle is drawn twice: by Win2D (together with the item blobs) and by the template's XAML Ellipse on top.
	// Transparency has to be applied to the Win2D layer as a whole: it's one surface, so circles overlapping inside it
	// (the main one and the collapsed items parked under it) don't stack up. A translucent brush would instead stack
	// with the Ellipse, show the parked items as a darker disc, and be forced opaque by the goo effect while animating.
	// So the Ellipse only keeps its outline, and the Win2D layer carries the fill and its opacity.
	private static void StyleTemplate(GooeyButton button)
	{
		if (VisualTree.FindDescendant<Ellipse>(button, x => x.Name == "BackgroundShape") is { } shape)
		{
			shape.Fill = new SolidColorBrush(Colors.Transparent); // transparent rather than null, so it stays hit-testable
			shape.Stroke = s_borderBrush;
			shape.StrokeThickness = BorderWidth;
		}

		if (VisualTree.FindDescendant<Canvas>(button, x => x.Name == "Win2DHost") is { } win2DHost)
		{
			win2DHost.Opacity = FillOpacity;
		}
	}

	private static readonly FieldInfo? s_gooeyEffectField = typeof(GooeyButton).GetField("image", BindingFlags.Instance | BindingFlags.NonPublic);

	// Workaround for a DevWinUI bug: while the items fan out, GooeyButton draws them through a Win2D "goo" threshold
	// (ColorMatrixEffect, alpha * 18 - 7) without ClampOutput, so alpha above 1 is multiplied back into the colour and
	// anything but a very dark colour blows out to white until the animation ends. Clamp it before the animation
	// starts (its Win2D resources are created when the button loads). Does nothing if DevWinUI's internals change.
	private static void ClampGooeyEffect(GooeyButton button)
	{
		if (s_gooeyEffectField?.GetValue(button) is ColorMatrixEffect { ClampOutput: false } effect)
		{
			effect.ClampOutput = true;
		}
	}

	// The whole button is shrunk by the Viewbox (80px -> 45px), so the icons need a larger font size than a
	// SymbolIcon's fixed 20px to stay legible. Symbol values are the glyph code points.
	private static FontIcon CreateIcon(Symbol symbol, double fontSize)
	{
		return new FontIcon
		{
			Glyph = ((char)symbol).ToString(),
			FontSize = fontSize,
		};
	}
}
