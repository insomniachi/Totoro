using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Dialogs;

/// <summary>
/// What the dialog host needs from any dialog view model, independent of its result type.
/// Derive from <see cref="DialogViewModel{TResult}"/> rather than implementing this directly.
/// </summary>
public interface IDialogViewModel
{
	string Title { get; }
	string PrimaryButtonText { get; }
	string CloseButtonText { get; }
	bool CanConfirm { get; }

	/// <summary>Called when the primary button is pressed. Return false to keep the dialog open (e.g. on a validation error).</summary>
	Task<bool> ConfirmAsync();
}

/// <summary>
/// Base class for dialog view models. Set <see cref="Result"/> in <see cref="ConfirmAsync"/>; it is what
/// <see cref="IDialogService.ShowAsync{TViewModel, TResult}"/> returns when the dialog is confirmed.
/// </summary>
public abstract partial class DialogViewModel<TResult> : ObservableObject, IDialogViewModel
{
	protected DialogViewModel()
	{
		Title = "";
		PrimaryButtonText = "OK";
		CloseButtonText = "Cancel";
		CanConfirm = true;
	}

	[ObservableProperty]
	public partial string Title { get; set; }

	[ObservableProperty]
	public partial string PrimaryButtonText { get; set; }

	[ObservableProperty]
	public partial string CloseButtonText { get; set; }

	/// <summary>Enables the primary button; bind validation to this.</summary>
	[ObservableProperty]
	public partial bool CanConfirm { get; set; }

	public TResult? Result { get; protected set; }

	public abstract Task<bool> ConfirmAsync();
}
