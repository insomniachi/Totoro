using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Behaviors;
using DevWinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Totoro.UI.Controls.AnimeCardWidgets;

public class HoverEffectsBehavior : BehaviorBase<AnimeCard>
{
	private EdgeLighting _lighting = null!;

	private static readonly AnimationSet _enterAnimation =
		[
			new ScaleAnimation()
			{
				EasingMode = EasingMode.EaseOut,
				EasingType = EasingType.Linear,
				To = "1.03",
				Duration = TimeSpan.FromMilliseconds(100)
			},
		];
	private static readonly AnimationSet _exitAnimation =
		[
			new ScaleAnimation()
			{
				EasingMode = EasingMode.EaseOut,
				EasingType = EasingType.Linear,
				To = "1",
				Duration = TimeSpan.FromMilliseconds(100)
			},
		];

	protected override void OnAttached()
	{
		var grid = (Grid)AssociatedObject.FindName("WidgetCanvas");
		_lighting = AssociatedObject.FindDescendant<EdgeLighting>()!;

		if (Application.Current.Resources.TryGetValue("SystemControlBackgroundAccentBrush", out var brushObj) && brushObj is SolidColorBrush brush)
		{
			_lighting.MidColor = brush.Color;
		}

		CommunityToolkit.WinUI.VisualExtensions.SetNormalizedCenterPoint(AssociatedObject, "0.5, 0.5");
		grid.PointerEntered += OnPointerEntered;
		grid.PointerExited += OnPointerExited;
	}

	private void OnPointerExited(object sender, PointerRoutedEventArgs e)
	{
		_exitAnimation.StartAsync(AssociatedObject);
		_lighting.StrokeThickness = 0;
		_lighting.IsLighting = false;
	}

	private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		_enterAnimation.StartAsync(AssociatedObject);
		_lighting.StrokeThickness = 5;
		_lighting.IsLighting = true;
	}
}