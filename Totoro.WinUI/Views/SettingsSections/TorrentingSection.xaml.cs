// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.Services;
using Totoro.Core.ViewModels;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class TorrentingSection : Page
{
    public SettingsViewModel ViewModel
    {
        get { return (SettingsViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(SettingsViewModel), typeof(TorrentingSection), new PropertyMetadata(null));

    public ICommand ResetProvider { get; }

    public TorrentingSection()
    {
        InitializeComponent();
        ResetProvider = ReactiveCommand.Create<string>(provider => App.GetService<IPluginOptionsStorage<ITorrentTracker>>().ResetConfig(provider));
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as SettingsViewModel;
    }
}
