// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.Torrents.Rss;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class RssSection : Page
{
    public SettingsViewModel ViewModel
    {
        get { return (SettingsViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(SettingsViewModel), typeof(RssSection), new PropertyMetadata(null));


    public RssSection()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as SettingsViewModel;
    }

    private void OnDelete(object sender, RoutedEventArgs e) 
    {
        //var btn = (Button)sender;
        //var item = (TorrentNameFilter)btn.Tag;
        //ViewModel.Settings.RssOptions.Filters.Remove(item);
    }
}
