// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SelectModelView : Page, IViewFor<ISelectModelViewModel>
{
    public SelectModelView()
    {
        InitializeComponent();
    }

    public ISelectModelViewModel ViewModel
    {
        get => DataContext as ISelectModelViewModel;
        set => DataContext = value;
    }

    object IViewFor.ViewModel
    {
        get => DataContext;
        set => DataContext = value;
    }
}
