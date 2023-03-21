using Microsoft.UI.Xaml.Controls;
using Splat;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class SettingsPageBase : ReactivePage<SettingsViewModel> { }
public sealed partial class SettingsPage : SettingsPageBase, IEnableLogger
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        try
        {
            var items = sender.ItemsSource as ObservableCollection<string>;
            for (int i = items.Count - 1; i >= args.Index + 1; i--)
            {
                items.RemoveAt(i);
            }
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
        }
    }
}
