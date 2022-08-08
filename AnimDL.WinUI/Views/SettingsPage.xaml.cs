using AnimDL.WinUI.ViewModels;

namespace AnimDL.WinUI.Views;

public class SettingsPageBase : ReactivePageEx<SettingsViewModel> { }
public sealed partial class SettingsPage : SettingsPageBase
{
    public SettingsPage()
    {
        InitializeComponent();
    }
}
