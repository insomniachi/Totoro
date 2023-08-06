using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;
using Totoro.WinUI.UserControls;

namespace Totoro.WinUI.Behaviors;

public class AdaptiveGridViewBehavior : Behavior<AdaptiveGridView>
{
    private CompositeDisposable _disposables = new();
    private ItemsWrapGrid _wrapGrid;

    public GridViewSettings Settings
    {
        get { return (GridViewSettings)GetValue(SettingsProperty); }
        set { SetValue(SettingsProperty, value); }
    }

    public static readonly DependencyProperty SettingsProperty =
        DependencyProperty.Register("Settings", typeof(GridViewSettings), typeof(AdaptiveGridViewBehavior), new PropertyMetadata(null, OnSettingsChanged));

    private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var behavior = d as AdaptiveGridViewBehavior;

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
                .Where(_ => behavior._wrapGrid is not null)
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
        for (int i = 0; i < AssociatedObject.Items.Count; i++)
        {
            if (AssociatedObject.ContainerFromIndex(i) is not GridViewItem item)
            {
                continue;
            }

            item.Margin = new Thickness(spacing);
        }
    }

    private void UpdateColumns(int count) => _wrapGrid.MaximumRowsOrColumns = count;
    
    private void UpdateHeight(double itemHeight) => AssociatedObject.ItemHeight = itemHeight;

    private void UpdateWidth(double desiredWidth) => AssociatedObject.DesiredWidth = desiredWidth;

    protected override void OnAttached()
    {
        AssociatedObject.Loaded += AssociatedObject_Loaded;
    }

    private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
    {
        _wrapGrid = (ItemsWrapGrid)AssociatedObject.ItemsPanelRoot;
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

