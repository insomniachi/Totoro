// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Plugins.Options;
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

public class ProviderOptionDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate TextBoxTemplate { get; set; }
    public DataTemplate ComboBoxTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item switch
        {
            SelectablePluginOption => ComboBoxTemplate,
            _ => TextBoxTemplate
        };
    }
}
