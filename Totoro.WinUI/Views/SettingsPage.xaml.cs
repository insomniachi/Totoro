using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class SettingsPageBase : ReactivePage<SettingsViewModel> { }
public sealed partial class SettingsPage : SettingsPageBase
{
    public SettingsPage()
    {
        InitializeComponent();
    }
}
