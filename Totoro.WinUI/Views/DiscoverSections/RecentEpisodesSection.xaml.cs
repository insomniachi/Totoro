using Totoro.Core.ViewModels.Discover;
using ReactiveMarbles.ObservableEvents;
using Totoro.Plugins.Anime.Contracts;
using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Views.DiscoverSections;

public class RecentEpisodesSectionBase : ReactivePage<RecentEpisodesViewModel> { }
public sealed partial class RecentEpisodesSection : RecentEpisodesSectionBase
{
    public RecentEpisodesSection()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            EpisodeView
            .Events()
            .ItemInvoked
            .Select(x => x.args.InvokedItem)
            .InvokeCommand(ViewModel.SelectEpisode);
        });
    }

    private void WatchExternalClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var episode = (IAiredAnimeEpisode)((MenuFlyoutItem)sender).Tag;
        Converters.WatchExternal.Execute(new Tuple<IAiredAnimeEpisode, string>(episode, ViewModel.SelectedProvider.Name));
    }
}
