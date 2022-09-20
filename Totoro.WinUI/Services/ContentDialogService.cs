using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Dialogs.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI;

namespace AnimDL.WinUI.Services;

public class ContentDialogService : IContentDialogService
{
    public async Task<ContentDialogResult> ShowDialog<TViewModel>(Action<ContentDialog> configure)
        where TViewModel : class
    {
        var vm = App.GetService<TViewModel>();
        return await ShowDialog(vm, configure);
    }

    public async Task<ContentDialogResult> ShowDialog<TViewModel>(TViewModel viewModel, Action<ContentDialog> configure)
        where TViewModel : class
    {
        var view = App.GetService<IViewFor<TViewModel>>();
        view.ViewModel = viewModel;
        var dialog = new ContentDialog()
        {
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
            Content = view
        };

        IDisposable disposable = null;
        if (viewModel is IClosable closeable)
        {
            disposable = closeable.Close.Subscribe(x => dialog.Hide());
        }

        configure(dialog);
        var result = await dialog.ShowAsync();
        if (disposable is not null)
        {
            disposable.Dispose();
        }
        if (viewModel is IDisposable d)
        {
            d.Dispose();
        }

        return result;
    }

    public Task<ContentDialogResult> ShowDialog<TViewModel>(Action<ContentDialog> configure, Action<TViewModel> configureVm) where TViewModel : class
    {
        var vm = App.GetService<TViewModel>();
        configureVm(vm);
        return ShowDialog(vm, configure);
    }
}
