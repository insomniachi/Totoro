// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;

public class ConfigureProviderViewBase : ReactivePage<ConfigureProviderViewModel> { }

public sealed partial class ConfigureProviderView : ConfigureProviderViewBase
{
    public ConfigureProviderView()
    {
        InitializeComponent();
    }
}
