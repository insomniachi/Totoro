// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class GridViewSection : Page
{

    public SettingsViewModel ViewModel
    {
        get { return (SettingsViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(SettingsViewModel), typeof(GridViewSection), new PropertyMetadata(null));

    public ICommand Reset { get; }

    public GridViewSection()
    {
        InitializeComponent();
        Preview.ItemsSource = Enumerable.Range(1, 6).Select(x => new object()).ToList();
        Reset = ReactiveCommand.Create(() => ViewModel.Settings.UserListGridViewSettings = new());
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as SettingsViewModel;
    }
}
