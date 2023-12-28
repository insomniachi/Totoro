using System.Collections;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;
using Splat;

namespace Totoro.WinUI.Behaviors;

public class AdaptiveItemsViewBehavior : Behavior<ItemsView>, IEnableLogger
{
    public int NumberOfColumns
    {
        get { return (int)GetValue(NumberOfColumnsProperty); }
        set { SetValue(NumberOfColumnsProperty, value); }
    }

    public double Spacing
    {
        get { return (double)GetValue(SpacingProperty); }
        set { SetValue(SpacingProperty, value); }
    }

    public double DesiredWidth
    {
        get { return (double)GetValue(DesiredWidthProperty); }
        set { SetValue(DesiredWidthProperty, value); }
    }

    public static readonly DependencyProperty DesiredWidthProperty =
        DependencyProperty.Register("DesiredWidth", typeof(double), typeof(AdaptiveItemsViewBehavior), new PropertyMetadata(0d));

    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register("Spacing", typeof(double), typeof(AdaptiveItemsViewBehavior), new PropertyMetadata(0d));

    public static readonly DependencyProperty NumberOfColumnsProperty =
        DependencyProperty.Register("NumberOfColumns", typeof(int), typeof(AdaptiveItemsViewBehavior), new PropertyMetadata(0));

    protected override void OnAttached()
    {
        UpdateWidthForDesiredWidth(AssociatedObject.Width);
        AssociatedObject.SizeChanged += AssociatedObject_SizeChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.SizeChanged -= AssociatedObject_SizeChanged;
    }

    private void AssociatedObject_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateWidthForDesiredWidth(e.NewSize.Width);
    }

    private void UpdateWidthForDesiredWidth(double width)
    {
        if(width == 0 || double.IsNaN(width))
        {
            return;
        }

        var columns = width / DesiredWidth;
        var intColumns = (int)columns;
        var adjustedWidth = (width - (intColumns - 1) * Spacing) / intColumns;

        //this.Log().Debug($"Adjusted Width : {adjustedWidth}");
        //this.Log().Debug($"Total Width : {width}; Calculated : {adjustedWidth * intColumns + (intColumns - 1) * Spacing}");
        
        //var totalSpacingWidth = (intColumns - 1) * Spacing;
        //var adjustedDesiredWidth = DesiredWidth - (totalSpacingWidth / intColumns);
        //var extraWidth = ((columns - intColumns) * adjustedDesiredWidth);

        //var newTotalWdith = totalSpacingWidth + (adjustedDesiredWidth + (extraWidth / intColumns)) * intColumns;

        foreach (var item in AssociatedObject.FindDescendants().OfType<ItemContainer>())
        {
            AssociatedObject.DispatcherQueue.TryEnqueue(() =>
            {
                if (((IList)AssociatedObject.ItemsSource).Count > intColumns)
                {
                    item.Width = adjustedWidth - 2;
                }
                else
                {
                    item.Width = DesiredWidth;
                }
            });
        }
    }
}
