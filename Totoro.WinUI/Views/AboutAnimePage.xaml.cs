using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;


public class AboutAnimePageBase : ReactivePage<AboutAnimeViewModel> { }

public sealed partial class AboutAnimePage : AboutAnimePageBase
{
    public AboutAnimePage()
    {
        InitializeComponent();
    }
}
