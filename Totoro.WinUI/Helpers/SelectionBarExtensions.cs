using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.WinUI.Helpers;

public class SelectionBarExtensions
{

    public static IEnumerable<object> GetItemSource(DependencyObject obj)
    {
        return (IEnumerable<object>)obj.GetValue(ItemSourceProperty);
    }

    public static void SetItemSource(DependencyObject obj, IEnumerable<object> value)
    {
        obj.SetValue(ItemSourceProperty, value);
    }

    public static readonly DependencyProperty ItemSourceProperty =
        DependencyProperty.RegisterAttached("ItemSource", typeof(IEnumerable<object>), typeof(SelectorBar), new PropertyMetadata(Enumerable.Empty<object>(), OnItemSourceChanged));

    private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var selectionBar = (SelectorBar)d;

        if (e.NewValue is not IEnumerable<object> values || !values.Any())
        {
            return;
        }

        selectionBar.DispatcherQueue.TryEnqueue(() =>
        {
            selectionBar.Items.Clear();
            foreach (var item in values)
            {
                selectionBar.Items.Add(new SelectorBarItem
                {
                    Text = item.ToString(),
                    FontSize = 20
                });
            }
        });
    }
}
