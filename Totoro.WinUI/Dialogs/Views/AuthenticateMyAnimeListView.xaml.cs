using AnimDL.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;

public class AuthenticateMyAnimeListViewBase : ReactivePage<AuthenticateMyAnimeListViewModel> { }
public sealed partial class AuthenticateMyAnimeListView : AuthenticateMyAnimeListViewBase
{
    public AuthenticateMyAnimeListView()
    {
        InitializeComponent();
    }
}
