using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace Totoro.WinUI.Helpers
{
    public static class OnScreenKeyboard
    {
        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(OnScreenKeyboard), new PropertyMetadata(false, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox || d is AutoSuggestBox)
            {
                var control = d as FrameworkElement;
                control.GotFocus += ControlGotFocus;
            }
        }

        private static void ControlGotFocus(object sender, RoutedEventArgs e)
        {
            Windows.UI.ViewManagement.InputPane inputPane = Windows.UI.ViewManagement.InputPaneInterop.GetForWindow(WindowNative.GetWindowHandle(App.MainWindow));
            inputPane.TryShow();
        }
    }
}
