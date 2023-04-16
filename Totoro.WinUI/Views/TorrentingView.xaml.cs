
using System.Diagnostics;
using CommunityToolkit.WinUI.UI.Controls;
using Humanizer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;
using Totoro.Core.Torrents;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Views.SettingsSections;

namespace Totoro.WinUI.Views;

public class TorrentingViewBase : ReactivePage<TorrentingViewModel> { }

public sealed partial class TorrentingView : TorrentingViewBase
{
    private static readonly NavigationTransitionInfo _fromLeft = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft };
    private static readonly NavigationTransitionInfo _fromRight = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
    private int _prevIndex = -1;

    public TorrentingView()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.SelectedSection)
                .WhereNotNull()
                .Subscribe(Navigate);
        });
    }

    private void Navigate(PivotItemModel section)
    {
        var type = section.Header switch
        {
            "Torrents" => typeof(SearchSection),
            "Downloads" => typeof(DownloadsSection),
            "Transfers" => typeof(TransfersSection),
            _ => null
        };

        if (type is null)
        {
            return;
        }

        var index = ViewModel.Sections.IndexOf(section);
        var transition = index > _prevIndex ? _fromLeft : _fromRight;
        _prevIndex = index;

        NavFrame.Navigate(type, ViewModel, transition);
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
            TorrentState.NotCached => "Cache",
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
