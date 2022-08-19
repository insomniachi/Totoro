using System;
using AnimDL.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AnimDL.WinUI.Helpers;

public static class Converters
{
    public static Visibility BooleanToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InvertedBooleanToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static bool Invert(bool value) => !value;
}