using Totoro.Core.ViewModels;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Views;

public class DiscoverPageBase : ReactivePage<DiscoverViewModel> { }
public sealed partial class DiscoverPage : DiscoverPageBase
{
    public DiscoverPage()
    {
        InitializeComponent();
    }
}