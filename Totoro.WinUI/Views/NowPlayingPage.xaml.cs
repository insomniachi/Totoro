using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.ViewModels;

namespace Totoro.WinUI.Views;

public class NowPlayingPageBase : ReactivePage<NowPlayingViewModel> { }

public sealed partial class NowPlayingPage : NowPlayingPageBase
{
    public NowPlayingPage()
    {
        InitializeComponent();
    }
}
