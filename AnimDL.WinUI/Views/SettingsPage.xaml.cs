using AnimDL.WinUI.ViewModels;
using ReactiveUI;

namespace AnimDL.WinUI.Views;

public class SettingsPageBase : ReactivePage<SettingsViewModel> { }
public sealed partial class SettingsPage : SettingsPageBase
{
    public SettingsPage()
    {
        InitializeComponent();
    }
}
