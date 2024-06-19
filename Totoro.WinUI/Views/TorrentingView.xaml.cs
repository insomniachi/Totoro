
using System.Diagnostics;
using Humanizer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.ViewModels;
using Totoro.Core.ViewModels.Torrenting;
using Totoro.Plugins.Torrents.Models;
using Totoro.WinUI.Contracts;

namespace Totoro.WinUI.Views;

public class TorrentingViewBase : ReactivePage<TorrentingViewModel> { }

public sealed partial class TorrentingView : TorrentingViewBase
{
    //private static readonly NavigationTransitionInfo _fromLeft = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft };
    //private static readonly NavigationTransitionInfo _fromRight = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
    //private int _prevIndex = -1;

    public TorrentingView()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if(e.Parameter is not IReadOnlyDictionary<string,object> parameters)
        {
            return;
        }

        var item = ViewModel.Sections.FirstOrDefault(x => x.ViewModel == typeof(SearchTorrentViewModel));
        item.NavigationParameters = parameters.ToDictionary();
        ViewModel.SelectedSection = item;
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
            TorrentState.Cached => "Play",
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
    public bool ShowRemainingTime { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is not null)
        {
            return value switch
            {
                DateTime d when d == new DateTime() => "-",
                DateTime d => (d - DateTime.Now).Humanize(2),
                _ => "-"
            };
        }

        return value switch
        {
            DateTime d when d == new DateTime() => "-",
            DateTime d => d.Humanize(),
            _ => "-"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
