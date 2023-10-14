using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace Totoro.WinUI.Behaviors;

public class ItemsViewBehavior : Behavior<ItemsView>
{
    private CompositeDisposable _disposables = new();

    public GridViewSettings Settings
    {
        get { return (GridViewSettings)GetValue(SettingsProperty); }
        set { SetValue(SettingsProperty, value); }
    }

    public static readonly DependencyProperty SettingsProperty =
        DependencyProperty.Register("Settings", typeof(GridViewSettings), typeof(ItemsViewBehavior), new PropertyMetadata(null, OnSettingsChanged));

    private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var behavior = d as ItemsViewBehavior;

        if(e.OldValue is { })
        {
            behavior._disposables.Dispose();
            behavior._disposables = new();
        }

        if (e.NewValue is not GridViewSettings settings)
        {
            return;
        }

        settings.WhenAnyValue(x => x.MaxNumberOfColumns)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(behavior.UpdateColumns)
                .DisposeWith(behavior._disposables);

        settings.WhenAnyValue(x => x.DesiredWidth)
                .Where(_ => behavior.AssociatedObject is not null)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(behavior.UpdateWidth)
                .DisposeWith(behavior._disposables);

        settings.WhenAnyValue(x => x.ItemHeight)
                .Where(_ => behavior.AssociatedObject is not null)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(behavior.UpdateHeight)
                .DisposeWith(behavior._disposables);

        settings.WhenAnyValue(x => x.SpacingBetweenItems)
                .Where(_ => behavior.AssociatedObject is not null)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(behavior.UpdateThickness)
                .DisposeWith(behavior._disposables);
    }

    private void UpdateThickness(double spacing)
    {
        var layout = AssociatedObject.Layout as UniformGridLayout;
        layout.MinColumnSpacing = spacing;
        layout.MinRowSpacing = spacing;
    }

    private void UpdateColumns(int count)
    {
        var layout = AssociatedObject.Layout as UniformGridLayout;
        layout.MaximumRowsOrColumns = count;
    }
    
    private void UpdateHeight(double itemHeight)
    {
        var layout = AssociatedObject.Layout as UniformGridLayout;
        layout.MinItemHeight = itemHeight;
    }

    private void UpdateWidth(double desiredWidth)
    {
        var layout = AssociatedObject.Layout as UniformGridLayout;
        layout.MinItemWidth = desiredWidth;
    }

    protected override void OnAttached()
    {
        AssociatedObject.Loaded += AssociatedObject_Loaded;
    }

    private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateColumns(Settings.MaxNumberOfColumns);
        UpdateThickness(Settings.SpacingBetweenItems);
        UpdateWidth(Settings.DesiredWidth);
        UpdateHeight(Settings.ItemHeight);
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= AssociatedObject_Loaded;

        if(_disposables.IsDisposed)
        {
            return;
        }

        _disposables.Dispose();
    }
}

