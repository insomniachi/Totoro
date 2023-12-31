using Totoro.Core.ViewModels.Discover;
using ReactiveMarbles.ObservableEvents;

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
}
