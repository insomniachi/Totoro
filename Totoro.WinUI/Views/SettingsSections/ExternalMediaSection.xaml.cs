// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core;
using Totoro.Core.ViewModels;
using Totoro.Plugins;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class ExternalMediaSection : Page, INotifyPropertyChanged
{
    public SettingsViewModel ViewModel
    {
        get { return (SettingsViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(SettingsViewModel), typeof(ExternalMediaSection), new PropertyMetadata(null));

    public event PropertyChangedEventHandler PropertyChanged;

    public IEnumerable<PluginInfo> MediaPlayers { get; } = PluginFactory<INativeMediaPlayer>.Instance.Plugins;
    public ICommand AddLibraryFolder { get; }

    private PluginInfo _selectedMediaPlayer;
    public PluginInfo SelectedMediaPlayer 
    {
        get => _selectedMediaPlayer;
        set
        {
            _selectedMediaPlayer = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedMediaPlayer)));
            if(value is not null)
            {
                App.GetService<ILocalSettingsService>().SaveSetting(Settings.DefaultMediaPlayer, value.Name);
            }
        }
    }

    private readonly ISettings _settings = App.GetService<ISettings>();

    public ExternalMediaSection()
    {
        InitializeComponent();
        SelectedMediaPlayer = PluginFactory<INativeMediaPlayer>.Instance.Plugins.FirstOrDefault(x => x.Name == _settings.DefaultMediaPlayer)
                ?? PluginFactory<INativeMediaPlayer>.Instance.Plugins.FirstOrDefault(x => x.Name == "vlc");

        AddLibraryFolder = ReactiveCommand.Create(async () =>
        {
            var folder = await App.GetService<IViewService>().BrowseFolder();
            
            if(string.IsNullOrEmpty(folder))
            {
                return;
            }

            ViewModel.Settings.LibraryFolders.Add(folder);
        });
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as SettingsViewModel;
    }
}
