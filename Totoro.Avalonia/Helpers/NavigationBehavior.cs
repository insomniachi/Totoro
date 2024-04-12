using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace Totoro.Avalonia.Helpers;

public class NavigationBehavior : AvaloniaObject
{
    private static readonly AttachedProperty<Type?> NavigateToProperty =
        AvaloniaProperty.RegisterAttached<NavigationBehavior, Interactive, Type?>("NavigateTo", default, false,
            BindingMode.OneTime, null);

    public static void SetNavigateTo(AvaloniaObject element, Type type)
    {
        element.SetValue(NavigateToProperty, type);
    }

    public static Type? GetNavigateTo(AvaloniaObject element)
    {
        return element.GetValue(NavigateToProperty);
    }
}

public class NavigationPageFactory : INavigationPageFactory
{
    public Control GetPage(Type srcType)
    {
        var viewType = typeof(IViewFor<>).MakeGenericType(srcType);
        var view = (Control)App.Services.GetService(viewType)!;
        view.DataContext = App.Services.GetService(srcType);
        return view;
    }

    public Control GetPageFromObject(object target)
    {
        var vmType = target.GetType();
        var viewType = typeof(IViewFor<>).MakeGenericType(vmType);
        var view = (Control)App.Services.GetService(viewType)!;
        view.DataContext = target;
        return view;
    }
}