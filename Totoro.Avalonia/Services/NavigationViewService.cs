using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using FluentAvalonia.UI.Controls;
using Totoro.Avalonia.Contracts;
using Totoro.Avalonia.Helpers;
using Totoro.Core.Contracts;

namespace Totoro.Avalonia.Services;

public class NavigationViewService(INavigationService navigationService) : INavigationViewService
{
    private NavigationView? _navigationView;
    private readonly INavigationService _navigationService = navigationService;

    public IList<object> MenuItems => _navigationView?.MenuItems ?? [];
    public object? SettingsItem => _navigationView?.SettingsItem;
    
    public void Initialize(NavigationView navigationView)
    {
        _navigationView = navigationView;
        _navigationView.BackRequested += OnBackRequested;
        _navigationView.ItemInvoked += OnItemInvoked;
    }

    public void UnregisterEvents()
    {
    }

    public NavigationViewItem? GetSelectedItem(Type vmType) => GetSelectedItem(MenuItems, vmType);
    
    private void OnBackRequested(object? sender, NavigationViewBackRequestedEventArgs args) => _navigationService.GoBack();

    private void OnItemInvoked(object? sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
        }
        else
        {

            if (args.InvokedItemContainer is not NavigationViewItem selectedItem)
            {
                return;
            }

            if (NavigationBehavior.GetNavigateTo(selectedItem) is not { } vmType)
            {
                return;
            }

            _navigationService.NavigateTo(vmType);
        }
    }
    
    private static NavigationViewItem? GetSelectedItem(IEnumerable<object> menuItems, Type vmType)
    {
        foreach (var item in menuItems.OfType<NavigationViewItem>())
        {
            if (IsMenuItemForPageType(item, vmType))
            {
                return item;
            }

            var selectedChild = GetSelectedItem(item.MenuItems, vmType);
            return selectedChild;
        }

        return null;
    }
    
    private static bool IsMenuItemForPageType(AvaloniaObject menuItem, Type vmType)
    {
        if (NavigationBehavior.GetNavigateTo(menuItem) is not { } type)
        {
            return false;
        }

        return type == vmType;
    }
}