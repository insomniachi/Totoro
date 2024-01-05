using Totoro.Core.ViewModels.About;

namespace Totoro.WinUI.Views.AboutSections;

public class OriginalSoundTracksSectionBase : ReactivePage<OriginalSoundTracksViewModel> { }

public sealed partial class OriginalSoundTracksSection : OriginalSoundTracksSectionBase
{
    public OriginalSoundTracksSection()
    {
        InitializeComponent();
    }
}
