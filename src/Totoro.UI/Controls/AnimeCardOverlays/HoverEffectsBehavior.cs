using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Behaviors;
using CommunityToolkit.WinUI.Media.Pipelines;
using DevWinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// On hover, the card grows slightly and its edge lights up in the accent color, while its image zooms in from its
/// center and blurs, so the actions over it stand out.
/// </summary>
/// <remarks>The blur is a backdrop brush, which the compositor redraws every frame, so it only exists while the card is hovered: on every card it slows scrolling.</remarks>
public class HoverEffectsBehavior : BehaviorBase<AnimeCard>
{
	private const float BlurAmount = 4;
	private static readonly TimeSpan s_imageDuration = TimeSpan.FromMilliseconds(250);

	private Grid? _canvas;
	private EdgeLighting? _lighting;
	private Image? _image;
	private SpriteVisual? _blurVisual;
	private EffectAnimation<float>? _blurAnimation;
	private int _hoverVersion;
	private PointerEventHandler? _onCaptureLost;

	private static readonly AnimationSet s_enterAnimation = CardScale("1.03");
	private static readonly AnimationSet s_exitAnimation = CardScale("1");
	private static readonly AnimationSet s_imageEnterAnimation = ImageScale("1.15");
	private static readonly AnimationSet s_imageExitAnimation = ImageScale("1");

	private static AnimationSet CardScale(string scale) =>
	[
		new ScaleAnimation { EasingMode = EasingMode.EaseOut, EasingType = EasingType.Linear, To = scale, Duration = TimeSpan.FromMilliseconds(100) },
	];

	private static AnimationSet ImageScale(string scale) =>
	[
		new ScaleAnimation { To = scale, Duration = s_imageDuration, EasingMode = EasingMode.EaseOut },
	];

	protected override void OnAttached()
	{
		base.OnAttached();

		// FindName works as soon as InitializeComponent has run. FindDescendant needs the
		// visual tree, which may not exist yet when the card comes from a DataTemplate.
		_canvas = AssociatedObject.FindName("OverlayLayer") as Grid;
		_lighting = AssociatedObject.FindName("Lighting") as EdgeLighting;
		_image = AssociatedObject.FindName("PosterImage") as Image;

		if (_canvas is null)
		{
			return;
		}

		if (_lighting is not null &&
			Application.Current.Resources.TryGetValue("SystemControlBackgroundAccentBrush", out var brushObj) && brushObj is SolidColorBrush brush)
		{
			_lighting.MidColor = brush.Color;
		}

		CommunityToolkit.WinUI.VisualExtensions.SetNormalizedCenterPoint(AssociatedObject, "0.5, 0.5");
		if (_image is not null)
		{
			CommunityToolkit.WinUI.VisualExtensions.SetNormalizedCenterPoint(_image, "0.5, 0.5");
		}

		_canvas.PointerEntered += OnPointerEntered;
		_canvas.PointerExited += OnPointerExited;
		HoverPointer.AddCaptureLostHandler(_canvas, _onCaptureLost = OnPointerCaptureLost);
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		if (_canvas is not null)
		{
			_canvas.PointerEntered -= OnPointerEntered;
			_canvas.PointerExited -= OnPointerExited;
			if (_onCaptureLost is not null)
			{
				HoverPointer.RemoveCaptureLostHandler(_canvas, _onCaptureLost);
			}
		}

		RemoveBlur();
		_onCaptureLost = null;
		_canvas = null;
		_lighting = null;
		_image = null;
	}

	private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
	{
		if (HoverPointer.IsOutside(sender, e))
		{
			OnPointerExited(sender, e);
		}
	}

	private async void OnPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		var version = ++_hoverVersion;
		s_enterAnimation.Start(AssociatedObject);
		if (_lighting is not null)
		{
			_lighting.StrokeThickness = 5;
			_lighting.IsLighting = true;
		}

		if (_image is null)
		{
			return;
		}

		s_imageEnterAnimation.Start(_image);
		if (_blurVisual is null)
		{
			var visual = await PipelineBuilder.FromBackdrop().Blur(0, out EffectAnimation<float> blurAnimation).AttachAsync(_image, _image); // the second one sizes the visual to the image; without it, it's 0x0
			if (version != _hoverVersion || _image is null)
			{
				visual.Dispose();
				return;
			}

			ElementCompositionPreview.SetElementChildVisual(_image, visual);
			_blurVisual = visual;
			_blurAnimation = blurAnimation;
		}

		await _blurAnimation!(_blurVisual.Brush, BlurAmount, s_imageDuration);
	}

	private async void OnPointerExited(object sender, PointerRoutedEventArgs e)
	{
		var version = ++_hoverVersion;
		s_exitAnimation.Start(AssociatedObject);
		if (_lighting is not null)
		{
			_lighting.StrokeThickness = 0;
			_lighting.IsLighting = false;
		}

		if (_image is null)
		{
			return;
		}

		s_imageExitAnimation.Start(_image);
		if (_blurVisual is null)
		{
			return;
		}

		await _blurAnimation!(_blurVisual.Brush, 0, s_imageDuration);
		if (version == _hoverVersion)
		{
			RemoveBlur();
		}
	}

	private void RemoveBlur()
	{
		if (_blurVisual is null)
		{
			return;
		}

		if (_image is not null)
		{
			ElementCompositionPreview.SetElementChildVisual(_image, null);
		}

		_blurVisual.Dispose();
		_blurVisual = null;
		_blurAnimation = null;
	}
}
