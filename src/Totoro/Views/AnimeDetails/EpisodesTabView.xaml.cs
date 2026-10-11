using System.Numerics;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.Views.AnimeDetails;

/// <summary>The Episodes tab.</summary>
public sealed partial class EpisodesTabView : UserControl
{
	private static readonly TimeSpan s_hoverDuration = TimeSpan.FromMilliseconds(150);

	public EpisodesTabView()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial EpisodesTab? ViewModel { get; set; }

	// The bar's track: the 160-wide thumbnail less its 8 margins. At least a few pixels, so a little progress still shows.
	private const double TrackWidth = 144;

	public static double ProgressWidth(double progress) => Math.Max(6, progress * TrackWidth);

	private void OnThumbnailPointerEntered(object sender, PointerRoutedEventArgs e) => AnimatePlayBadge(sender, hovered: true);

	private void OnThumbnailPointerExited(object sender, PointerRoutedEventArgs e) => AnimatePlayBadge(sender, hovered: false);

	/// <summary>Grows the thumbnail's play button and fills it with the accent colour while the pointer is over it.</summary>
	private static void AnimatePlayBadge(object sender, bool hovered)
	{
		if (sender is not FrameworkElement thumbnail ||
			thumbnail.FindDescendant("PlayBadge") is not FrameworkElement badge ||
			thumbnail.FindDescendant("PlayHighlight") is not FrameworkElement highlight)
		{
			return;
		}

		// Scale about the middle, not the top-left corner.
		ElementCompositionPreview.GetElementVisual(badge).CenterPoint = new Vector3((float)badge.ActualWidth / 2, (float)badge.ActualHeight / 2, 0);

		AnimationBuilder.Create()
						.Scale(to: hovered ? 1.25 : 1, duration: s_hoverDuration, easingMode: EasingMode.EaseOut)
						.Start(badge);
		AnimationBuilder.Create()
						.Opacity(to: hovered ? 1 : 0, duration: s_hoverDuration, layer: FrameworkLayer.Xaml)
						.Start(highlight);
	}
}
