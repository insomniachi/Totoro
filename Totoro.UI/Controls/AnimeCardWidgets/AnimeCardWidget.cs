using CommunityToolkit.WinUI.Behaviors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls.AnimeCardWidgets;

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

		if (Control is not null)
		{
			OnHiding(Control);
		}

		Control?.Visibility = Visibility.Collapsed;
		grid.Children.Remove(Control);
		Control = null;

		OnPointerExited();
	}


	protected abstract TControl CreateWidget();

	/// <summary>Called right before the widget is collapsed and removed, i.e. before its hide animation runs.</summary>
	protected virtual void OnHiding(TControl widget) { }

	protected virtual void OnPointerEntered() { }

	protected virtual void OnPointerExited() { }
}
