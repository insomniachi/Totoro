using AnimDL.WinUI.ViewModels;

namespace AnimDL.WinUI.Views;

public class DiscoverPageBase : ReactivePageEx<DiscoverViewModel> { }
public sealed partial class DiscoverPage : DiscoverPageBase
{
    public DiscoverPage()
    {
        InitializeComponent();
    }
}
