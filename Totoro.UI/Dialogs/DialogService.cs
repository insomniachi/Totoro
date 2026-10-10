using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Dialogs;
using Totoro.Core.Navigation;

namespace Totoro.UI.Dialogs;

/// <summary>
/// Shows dialogs registered with <c>AddDialog</c> in a <see cref="ContentDialog"/>. The view and view model are created
/// the same way pages are (view model from a DI scope, assigned to the view's <c>ViewModel</c> property); the dialog's
/// title and buttons follow the view model's <see cref="IDialogViewModel"/> properties.
/// </summary>
public sealed class DialogService(IViewRegistry viewRegistry, IServiceScopeFactory scopeFactory, Func<XamlRoot?> xamlRoot) : IDialogService
{
	public async Task<TResult?> ShowAsync<TViewModel, TResult>(object? parameter = null)
		where TViewModel : DialogViewModel<TResult>
	{
		if (viewRegistry.FindByViewModel(typeof(TViewModel)) is not { } map)
		{
			throw new InvalidOperationException($"No dialog registered for {typeof(TViewModel).Name}; call AddDialog<TView, {typeof(TViewModel).Name}>().");
		}

		// The view model lives as long as the dialog is open.
		using var scope = scopeFactory.CreateScope();
		var viewModel = parameter is null
			? ActivatorUtilities.CreateInstance<TViewModel>(scope.ServiceProvider)
			: ActivatorUtilities.CreateInstance<TViewModel>(scope.ServiceProvider, parameter);

		var view = (FrameworkElement)Activator.CreateInstance(map.View)!;
		view.DataContext = viewModel;
		map.View.GetProperty("ViewModel")?.SetValue(view, viewModel);

		var dialog = new ContentDialog
		{
			XamlRoot = xamlRoot() ?? throw new InvalidOperationException("Dialogs can't be shown before the main window has loaded."),
			Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
			DefaultButton = ContentDialogButton.Primary,
			Content = view,
		};

		void Sync(object? sender = null, PropertyChangedEventArgs? e = null)
		{
			dialog.Title = viewModel.Title;
			dialog.PrimaryButtonText = viewModel.PrimaryButtonText;
			dialog.CloseButtonText = viewModel.CloseButtonText;
			dialog.IsPrimaryButtonEnabled = viewModel.CanConfirm;
		}

		Sync();
		viewModel.PropertyChanged += Sync;

		dialog.PrimaryButtonClick += async (_, args) =>
		{
			var deferral = args.GetDeferral();
			try
			{
				args.Cancel = !await viewModel.ConfirmAsync();
			}
			finally
			{
				deferral.Complete();
			}
		};

		try
		{
			return await dialog.ShowAsync() == ContentDialogResult.Primary ? viewModel.Result : default;
		}
		finally
		{
			viewModel.PropertyChanged -= Sync;
		}
	}
}
