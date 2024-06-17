using Totoro.Core.ViewModels.About;

namespace Totoro.WinUI.Views.AboutSections;

public class EpisodesSectionBase : ReactivePage<AnimeEpisodesViewModel> { }

public sealed partial class EpisodesSection : EpisodesSectionBase
{
    public EpisodesSection()
    {
        InitializeComponent();
    }

    private void EpisodesView_ItemInvoked(Microsoft.UI.Xaml.Controls.ItemsView sender, Microsoft.UI.Xaml.Controls.ItemsViewItemInvokedEventArgs args)
    {
        ViewModel.Watch.Execute(args.InvokedItem);
    }
}
