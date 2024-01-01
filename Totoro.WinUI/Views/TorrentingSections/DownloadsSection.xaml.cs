using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Totoro.Core.ViewModels.Torrenting;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Views.TorrentingSections;

public class DownloadsSectionBase : ReactivePage<TorrentDownloadsViewModel> { }

public sealed partial class DownloadsSection : DownloadsSectionBase
{
    public DownloadsSection()
    {
        InitializeComponent();
    }
}


public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not bool b)
        {
            return DependencyProperty.UnsetValue;
        }

        return Converters.BooleanToVisibility(b);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
