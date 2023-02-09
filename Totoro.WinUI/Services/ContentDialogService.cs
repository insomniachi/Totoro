using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Services;

public class ContentDialogService : IContentDialogService
{
    public async Task<ContentDialogResult> ShowDialog<TViewModel>(Action<ContentDialog> configure)
        where TViewModel : class
    {
        var vm = App.GetService<TViewModel>();
        return await ShowDialog(vm, configure);
    }

    public async Task<ContentDialogResult> ShowDialog<TView, TViewModel>(TViewModel viewModel, Action<ContentDialog> configure)
        where TView : IViewFor, new()
    {
        var view = new TView
        {
            ViewModel = viewModel
        };
        var dialog = new ContentDialog()
        {
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
            Content = view,
            ManipulationMode = Microsoft.UI.Xaml.Input.ManipulationModes.All
        };

        dialog.ManipulationDelta += delegate (object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (!e.IsInertial)
            {
                dialog.Margin = new Thickness(dialog.Margin.Left + e.Delta.Translation.X,
                                              dialog.Margin.Top + e.Delta.Translation.Y,
                                              dialog.Margin.Left - e.Delta.Translation.X,
                                              dialog.Margin.Top - e.Delta.Translation.Y);
            }
        };

        IDisposable disposable = null;
        if (viewModel is IClosable closeable)
        {
            disposable = closeable.Close.Subscribe(x => dialog.Hide());
        }

        configure(dialog);
        var result = await dialog.ShowAsync();
        disposable?.Dispose();
        if (viewModel is IDisposable d)
        {
            d.Dispose();
        }

        return result;
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
            Content = view,
            ManipulationMode = Microsoft.UI.Xaml.Input.ManipulationModes.All
        };

        dialog.ManipulationDelta += delegate (object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (!e.IsInertial)
            {
                dialog.Margin = new Thickness(dialog.Margin.Left + e.Delta.Translation.X,
                                              dialog.Margin.Top + e.Delta.Translation.Y,
                                              dialog.Margin.Left - e.Delta.Translation.X,
                                              dialog.Margin.Top - e.Delta.Translation.Y);
            }
        };

        IDisposable disposable = null;
        if (viewModel is IClosable closeable)
        {
            disposable = closeable.Close.Subscribe(x => dialog.Hide());
        }

        configure(dialog);
        var result = await dialog.ShowAsync();
        disposable?.Dispose();
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
