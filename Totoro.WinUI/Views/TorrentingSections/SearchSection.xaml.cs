// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;
using Totoro.Core.ViewModels.Torrenting;
using Windows.ApplicationModel.DataTransfer;
using TorrentModel = Totoro.Plugins.Torrents.Models.TorrentModel;

namespace Totoro.WinUI.Views.TorrentingSections;

public class SearchSectionBase : ReactivePage<SearchTorrentViewModel> { }

public sealed partial class SearchSection : SearchSectionBase
{
    public SearchSection()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            SearchBox
            .Events()
            .QuerySubmitted
            .Select(_ => Unit.Default)
            .InvokeCommand(ViewModel.Search)
            .DisposeWith(d);

            this.WhenAnyValue(x => x.ViewModel.ProviderType)
                .WhereNotNull()
                .Subscribe(type =>
                {
                    switch (type)
                    {
                        case "nya":
                            foreach (var item in DataGrid.Columns)
                            {
                                item.Visibility = Visibility.Visible;
                            }
                            break;
                    }
                });
        });
    }

    private void TorrentAction(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button.DataContext is not TorrentModel m)
        {
            return;
        }

        App.Commands.StreamWithDebrid.Execute(m);
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

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button.DataContext is not TorrentModel m)
        {
            return;
        }

        App.Commands.DownloadTorrentCommand.Execute(m);
    }

    private void StreamButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button.DataContext is not TorrentModel m)
        {
            return;
        }

        App.Commands.TorrentCommand.Execute(m);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button.DataContext is not TorrentModel m)
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(m.Magnet);
        Clipboard.SetContent(package);
    }
}
