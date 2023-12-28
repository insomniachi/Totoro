using System.Windows;
using Microsoft.UI.Xaml.Controls;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class DiscoverPageBase : ReactivePage<DiscoverViewModel> { }
public sealed partial class DiscoverPage : DiscoverPageBase
{
    public DiscoverPage()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            ProviderSearchBox
            .Events()
            .QuerySubmitted
            .Select(x => x.sender.Text)
            .InvokeCommand(ViewModel.SearchProvider);

            SearchResultView
            .Events()
            .ItemInvoked
            .Select(x => x.args.InvokedItem)
            .InvokeCommand(ViewModel.SelectSearchResult);

            EpisodeView
            .Events()
            .ItemInvoked
            .Select(x => x.args.InvokedItem)
            .InvokeCommand(ViewModel.SelectEpisode);

        });
    }

    private void MainGrid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var element = (Grid)sender;
        element.Width = ViewModel.CardWidth;
    }
}
