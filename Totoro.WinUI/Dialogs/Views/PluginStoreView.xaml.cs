using Microsoft.UI.Xaml;
using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;


public class PluginStoreViewBase : ReactivePage<PluginStoreViewModel> { }

public sealed partial class PluginStoreView : PluginStoreViewBase
{
    private readonly IKnownFolders _knownFolders = App.GetService<IKnownFolders>();
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
