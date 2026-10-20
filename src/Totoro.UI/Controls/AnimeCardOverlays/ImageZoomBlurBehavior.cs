using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Behaviors;
using CommunityToolkit.WinUI.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>On hover, zooms the card's image in slightly from its center and blurs it, both animated together.</summary>
public class ImageZoomBlurBehavior : BehaviorBase<AnimeCard>
{
	private static readonly TimeSpan s_duration = TimeSpan.FromMilliseconds(250);

	private Image? _image;
	private AnimationSet? _enterAnimation;
	private AnimationSet? _exitAnimation;

	protected override void OnAttached()
	{
		base.OnAttached();

		// FindName works before the card is in the visual tree (see HoverEffectsBehavior).
		_image = AssociatedObject.FindName("PosterImage") as Image;
		if (_image is null)
		{
			return;
		}

		var blur = new BlurEffect { IsAnimatable = true, Amount = 0 };
		CommunityToolkit.WinUI.Media.UIElementExtensions.SetVisualFactory(_image, new PipelineVisualFactory { Effects = { blur } });
		VisualExtensions.SetNormalizedCenterPoint(_image, "0.5, 0.5");

		_enterAnimation = Animation("1.15", 4);
		_exitAnimation = Animation("1", 0);

		AnimationSet Animation(string scale, double blurAmount) =>
		[
			new ScaleAnimation { To = scale, Duration = s_duration, EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut },
			new BlurEffectAnimation { Target = blur, To = blurAmount, Duration = s_duration, EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut },
		];

		AssociatedObject.PointerEntered += OnPointerEntered;
		AssociatedObject.PointerExited += OnPointerExited;
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		AssociatedObject.PointerEntered -= OnPointerEntered;
		AssociatedObject.PointerExited -= OnPointerExited;
		_image = null;
	}

	private void OnPointerEntered(object sender, PointerRoutedEventArgs e) => _enterAnimation?.Start(_image!);

	private void OnPointerExited(object sender, PointerRoutedEventArgs e) => _exitAnimation?.Start(_image!);
}
