// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SelectModelView : Page, IViewFor<SelectModelViewModel>
{
    public SelectModelView()
    {
        InitializeComponent();
    }

    public SelectModelViewModel ViewModel
    {
        get => DataContext as SelectModelViewModel;
        set => DataContext = value;
    }

    object IViewFor.ViewModel { get => ViewModel; set => ViewModel = value as SelectModelViewModel; }

    private void ComboBox_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        (sender as ComboBox).SelectedIndex = 0;
    }
}
