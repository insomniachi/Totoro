using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Totoro.Core.ViewModels.Discover;

namespace Totoro.Avalonia.Views.DiscoverSections;

public partial class RecentEpisodesView : ReactiveUserControl<RecentEpisodesViewModel>
{
    public RecentEpisodesView()
    {
        InitializeComponent();
    }
}