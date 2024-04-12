using System;
using System.Collections.Generic;
using FluentAvalonia.UI.Controls;

namespace Totoro.Avalonia.Contracts;

public interface INavigationViewService
{
    IList<object> MenuItems { get; }

    object? SettingsItem { get; }

    void Initialize(NavigationView navigationView);

    void UnregisterEvents();

    NavigationViewItem? GetSelectedItem(Type vmType);
}