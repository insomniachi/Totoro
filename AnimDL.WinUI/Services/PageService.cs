using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.ViewModels;
using AnimDL.WinUI.Views;
using Microsoft.UI.Xaml.Controls;

namespace AnimDL.WinUI.Services;

public class PageService : IPageService
{
    private readonly Dictionary<string, Type> _pages = new();

    public PageService()
    {
        Configure<UserListViewModel, UserListPage>();
        Configure<SettingsViewModel, SettingsPage>();
        Configure<WatchViewModel, WatchPage>();
        Configure<SeasonalViewModel, SeasonalPage>();
        Configure<ScheduleViewModel, SchedulePage>();
    }

    public Type GetPageType(string key)
    {
        Type pageType;
        lock (_pages)
        {
            if (!_pages.TryGetValue(key, out pageType))
            {
                throw new ArgumentException($"Page not found: {key}. Did you forget to call PageService.Configure?");
            }
        }

        return pageType;
    }

    private void Configure<VM, V>()
        where VM : INotifyPropertyChanged
        where V : Page
    {
        lock (_pages)
        {
            var key = typeof(VM).FullName;
            if (_pages.ContainsKey(key))
            {
                throw new ArgumentException($"The key {key} is already configured in PageService");
            }

            var type = typeof(V);
            if (_pages.Any(p => p.Value == type))
            {
                throw new ArgumentException($"This type is already configured with key {_pages.First(p => p.Value == type).Key}");
            }

            _pages.Add(key, type);
        }
    }
}
