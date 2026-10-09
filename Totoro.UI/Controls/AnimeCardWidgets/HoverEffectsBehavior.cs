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
	private Grid? _canvas;
	private EdgeLighting? _lighting;

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
		base.OnAttached();

		// FindName works as soon as InitializeComponent has run. FindDescendant needs the
		// visual tree, which may not exist yet when the card comes from a DataTemplate.
		_canvas = AssociatedObject.FindName("WidgetCanvas") as Grid;
		_lighting = AssociatedObject.FindName("Lighting") as EdgeLighting;

		if (_canvas is null || _lighting is null)
		{
			return;
		}

		if (Application.Current.Resources.TryGetValue("SystemControlBackgroundAccentBrush", out var brushObj) && brushObj is SolidColorBrush brush)
		{
			_lighting.MidColor = brush.Color;
		}

		CommunityToolkit.WinUI.VisualExtensions.SetNormalizedCenterPoint(AssociatedObject, "0.5, 0.5");
		_canvas.PointerEntered += OnPointerEntered;
		_canvas.PointerExited += OnPointerExited;
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		if (_canvas is not null)
		{
			_canvas.PointerEntered -= OnPointerEntered;
			_canvas.PointerExited -= OnPointerExited;
		}

		_canvas = null;
		_lighting = null;
	}

	private void OnPointerExited(object sender, PointerRoutedEventArgs e)
	{
		_exitAnimation.StartAsync(AssociatedObject);
		if (_lighting is not null)
		{
			_lighting.StrokeThickness = 0;
			_lighting.IsLighting = false;
		}
	}

	private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		_enterAnimation.StartAsync(AssociatedObject);
		if (_lighting is not null)
		{
			_lighting.StrokeThickness = 5;
			_lighting.IsLighting = true;
		}
	}
}