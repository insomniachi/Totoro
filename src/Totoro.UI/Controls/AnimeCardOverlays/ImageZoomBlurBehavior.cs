using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Behaviors;
using CommunityToolkit.WinUI.Media.Pipelines;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>On hover, zooms the card's image in slightly from its center and blurs it, both animated together.</summary>
/// <remarks>The blur is a backdrop brush, which the compositor redraws every frame, so it only exists while the card is hovered: on every card it slows scrolling.</remarks>
public class ImageZoomBlurBehavior : BehaviorBase<AnimeCard>
{
	private const float BlurAmount = 4;
	private static readonly TimeSpan s_duration = TimeSpan.FromMilliseconds(250);

	private Image? _image;
	private AnimationSet? _enterAnimation;
	private AnimationSet? _exitAnimation;
	private SpriteVisual? _blurVisual;
	private EffectAnimation<float>? _blurAnimation;
	private int _hoverVersion;

	protected override void OnAttached()
	{
		base.OnAttached();

		// FindName works before the card is in the visual tree (see HoverEffectsBehavior).
		_image = AssociatedObject.FindName("PosterImage") as Image;
		if (_image is null)
		{
			return;
		}

		VisualExtensions.SetNormalizedCenterPoint(_image, "0.5, 0.5");

		_enterAnimation = Animation("1.15");
		_exitAnimation = Animation("1");

		static AnimationSet Animation(string scale) =>
		[
			new ScaleAnimation { To = scale, Duration = s_duration, EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut },
		];

		AssociatedObject.PointerEntered += OnPointerEntered;
		AssociatedObject.PointerExited += OnPointerExited;
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		AssociatedObject.PointerEntered -= OnPointerEntered;
		AssociatedObject.PointerExited -= OnPointerExited;
		RemoveBlur();
		_image = null;
	}

	private async void OnPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (_image is null)
		{
			return;
		}

		var version = ++_hoverVersion;
		_enterAnimation?.Start(_image);

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

		await _blurAnimation!(_blurVisual.Brush, BlurAmount, s_duration);
	}

	private async void OnPointerExited(object sender, PointerRoutedEventArgs e)
	{
		if (_image is null)
		{
			return;
		}

		var version = ++_hoverVersion;
		_exitAnimation?.Start(_image);

		if (_blurVisual is null)
		{
			return;
		}

		await _blurAnimation!(_blurVisual.Brush, 0, s_duration);
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
