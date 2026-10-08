using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Behaviors;
using DevWinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls.AnimeCardWidgets;

public class FloatingActionButtonWidget : AnimeCardWidget<Viewbox>
{
	protected override Viewbox CreateWidget()
	{
		var control = new GooeyButton()
		{
			Content = new SymbolIcon(Symbol.More),
		};

		control.ItemInvoked += OnInvoked;
		control.Items.Add(CreateItem(Symbol.Play));
		control.Items.Add(CreateItem(Symbol.Edit));
		control.Items.Add(CreateItem(Symbol.Download));

		var vb = new Viewbox()
		{
			Child = control,
			Height = 50,
			Width = 50,
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
		;
	}

	private static GooeyButtonItem CreateItem(Symbol symbol)
	{
		return new GooeyButtonItem()
		{
			Content = new SymbolIcon(symbol)
		};
	}
}

public abstract class AnimeCardWidget<TControl> : BehaviorBase<AnimeCard>
	where TControl : UIElement
{

	TControl? Control { get; set; }

	protected override void OnAttached()
	{
		var grid = AssociatedObject.FindName("WidgetCanvas") as Microsoft.UI.Xaml.Controls.Grid;
		if (grid is null)
		{
			return;
		}

		grid.PointerEntered += OnPointerEntered;
		grid.PointerExited += OnPointerExited;
	}

	protected override void OnDetaching()
	{
		var grid = AssociatedObject.FindName("WidgetCanvas") as Microsoft.UI.Xaml.Controls.Grid;
		if (grid is null)
		{
			return;
		}

		grid.PointerEntered -= OnPointerEntered;
		grid.PointerExited -= OnPointerExited;
	}

	private void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if(sender is not Grid grid)
		{
			return;
		}

		Control = CreateWidget();
		grid.Children.Add(Control);

		OnPointerEntered();
	}

	private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (sender is not Grid grid)
		{
			return;
		}

		Control?.Visibility = Visibility.Collapsed;
		grid.Children.Remove(Control);
		Control = null;

		OnPointerExited();
	}


	protected abstract TControl CreateWidget();

	protected virtual void OnPointerEntered() { }

	protected virtual void OnPointerExited() { }
}
