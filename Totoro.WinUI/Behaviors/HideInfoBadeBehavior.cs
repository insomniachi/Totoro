using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;

namespace Totoro.WinUI.Behaviors;

public class HideInfoBadeBehavior : Behavior<InfoBadge>
{
    private long _token;

    protected override void OnAttached()
    {
        AssociatedObject.Visibility = Visibility.Collapsed;
        _token = AssociatedObject.RegisterPropertyChangedCallback(InfoBadge.ValueProperty, OnValueChanged);
    }

    protected override void OnDetaching()
    {
        AssociatedObject.UnregisterPropertyChangedCallback(InfoBadge.ValueProperty, _token);
    }

    private void OnValueChanged(DependencyObject sender, DependencyProperty dp)
    {
        var infoBadge = (InfoBadge)sender;
        var value = (int)infoBadge.GetValue(dp);
        infoBadge.Visibility = value > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
