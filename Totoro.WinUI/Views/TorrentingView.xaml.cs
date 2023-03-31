
using System.Diagnostics;
using CommunityToolkit.WinUI.UI.Controls;
using Humanizer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core.Torrents;
using Totoro.Core.ViewModels;
using TorrentModel = Totoro.Core.Torrents.TorrentModel;

namespace Totoro.WinUI.Views;

public class TorrentingViewBase : ReactivePage<TorrentingViewModel> { }

public sealed partial class TorrentingView : TorrentingViewBase
{
    public TorrentingView()
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
}

public class TorrentStateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TorrentState state)
        {
            return DependencyProperty.UnsetValue;
        }

        return state switch
        {
            TorrentState.Unknown => "Try Play",
            TorrentState.NotCached => "Download",
            TorrentState.Requested => "Caching",
            _ => throw new UnreachableException()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class HumanizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            DateTime d => d.Humanize(),
            _ => DependencyProperty.UnsetValue
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
