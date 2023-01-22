using Microsoft.UI.Xaml.Controls;

namespace Totoro.WinUI.Contracts;

public interface IContentDialogService
{
    Task<ContentDialogResult> ShowDialog<TViewModel>(Action<ContentDialog> configure) where TViewModel : class;
    Task<ContentDialogResult> ShowDialog<TViewModel>(TViewModel viewModel, Action<ContentDialog> configure) where TViewModel : class;
    Task<ContentDialogResult> ShowDialog<TViewModel>(Action<ContentDialog> configure, Action<TViewModel> configureVm) where TViewModel : class;
    Task<ContentDialogResult> ShowDialog<TView, TViewModel>(TViewModel viewModel, Action<ContentDialog> configure) where TView : IViewFor, new();
}
