using Totoro.WinUI.Dialogs.ViewModels;


namespace Totoro.WinUI.Dialogs.Views;

public class AuthenticateSimklViewBase : ReactivePage<AuthenticateSimklViewModel> { }

public sealed partial class AuthenticateSimklView : AuthenticateSimklViewBase
{
    public AuthenticateSimklView()
    {
        InitializeComponent();
    }
}
