// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class DownloadsSection : Page
{
    public TorrentingViewModel ViewModel
    {
        get { return (TorrentingViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(TorrentingViewModel), typeof(DownloadsSection), new PropertyMetadata(null));


    public DownloadsSection()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as TorrentingViewModel;
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
