using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Totoro.Core.ViewModels;

namespace Totoro.Avalonia.Views;

public partial class DiscoverView : ReactiveUserControl<DiscoverViewModel>
{
    public DiscoverView()
    {
        InitializeComponent();
    }
}