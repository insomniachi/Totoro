// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace Totoro.WinUI.Dialogs.Views;


public sealed partial class ConfigureOptionsView : Page, IViewFor
{
    public ConfigureOptionsView()
    {
        InitializeComponent();
    }

    public object ViewModel
    {
        get => DataContext;
        set => DataContext = value;
    }
}
