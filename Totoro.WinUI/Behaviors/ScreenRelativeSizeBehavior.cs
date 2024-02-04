using Microsoft.UI.Xaml;
using Microsoft.Xaml.Interactivity;

namespace Totoro.WinUI.Behaviors;

public class ScreenRelativeSizeBehavior : Behavior<FrameworkElement>
{
    public double Factor { get; set; }

    protected override void OnAttached()
    {
        AssociatedObject.Loaded += AssociatedObject_Loaded;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= AssociatedObject_Loaded;
    }

    private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
    {
        AssociatedObject.Height = App.MainWindow.Height * Factor;
    }
}
