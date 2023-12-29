using Microsoft.UI.Xaml;
using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.UserControls;

namespace Totoro.WinUI.Dialogs.Views;


public class PluginStoreViewBase : ReactiveContentDialog<PluginStoreViewModel> { }

public sealed partial class PluginStoreView : PluginStoreViewBase
{
    public PluginStoreView()
    {
        InitializeComponent();

        DownloadPlugin = ReactiveCommand.Create<string>(async file => await ViewModel.DownloadPlugin(file));
    }

    public ICommand DownloadPlugin { get; }
    public ICommand UninstallPlugin { get; }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.DownloadPlugin(((FrameworkElement)sender).Tag as string);
        }
        catch { }
    }
}
