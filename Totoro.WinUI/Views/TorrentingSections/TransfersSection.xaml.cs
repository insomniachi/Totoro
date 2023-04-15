// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class TransfersSection : Page
{
    public TorrentingViewModel ViewModel
    {
        get { return (TorrentingViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(TorrentingViewModel), typeof(TransfersSection), new PropertyMetadata(null));


    public TransfersSection()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as TorrentingViewModel;
    }
}
