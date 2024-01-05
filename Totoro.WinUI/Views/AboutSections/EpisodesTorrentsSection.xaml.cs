using Totoro.Core.ViewModels.About;

namespace Totoro.WinUI.Views.AboutSections;

public class EpisodesTorrentsSectionBase : ReactivePage<AnimeEpisodesTorrentViewModel> { }

public sealed partial class EpisodesTorrentsSection : EpisodesTorrentsSectionBase
{
    public EpisodesTorrentsSection()
    {
        InitializeComponent();
    }
}
