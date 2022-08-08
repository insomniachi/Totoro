using AnimDL.WinUI.Dialogs.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReactiveUI;


namespace AnimDL.WinUI.Dialogs.Views;

public class AuthenticateMyAnimeListViewBase : ReactivePage<AuthenticateMyAnimeListViewModel> { }
public sealed partial class AuthenticateMyAnimeListView : AuthenticateMyAnimeListViewBase
{
    public AuthenticateMyAnimeListView()
    {
        InitializeComponent();
    }
}
