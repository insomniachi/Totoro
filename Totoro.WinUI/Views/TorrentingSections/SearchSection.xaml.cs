// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core.Torrents;
using Totoro.Core.ViewModels;
using TorrentModel = Totoro.Core.Torrents.TorrentModel;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class SearchSection : Page, IViewFor<TorrentingViewModel>
{
    public TorrentingViewModel ViewModel
    {
        get { return (TorrentingViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    object IViewFor.ViewModel { get => ViewModel; set => ViewModel = value as TorrentingViewModel; }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(TorrentingViewModel), typeof(SearchSection), new PropertyMetadata(null));


    public SearchSection()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            SearchBox
            .Events()
            .QuerySubmitted
            .Select(_ => System.Reactive.Unit.Default)
            .InvokeCommand(ViewModel.Search)
            .DisposeWith(d);

            switch (ViewModel.ProviderType)
            {
                case TorrentProviderType.Nya:
                    foreach (var item in DataGrid.Columns)
                    {
                        item.Visibility = Visibility.Visible;
                    }
                    break;
            }
        });
    }

    private void TorrentAction(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button.DataContext is not TorrentModel m)
        {
            return;
        }

        App.Commands.TorrentCommand.Execute(m);
    }

    private void DataGrid_Sorting(object sender, DataGridColumnEventArgs e)
    {
        if (e.Column.Tag is "Time")
        {
            ViewModel.SortMode = SortMode.Date;
        }
        else if (e.Column.Tag is "Seeders")
        {
            ViewModel.SortMode = SortMode.Seeders;
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as TorrentingViewModel;
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button.DataContext is not TorrentModel m)
        {
            return;
        }

        App.Commands.DownloadTorrentCommand.Execute(m);
    }
}
