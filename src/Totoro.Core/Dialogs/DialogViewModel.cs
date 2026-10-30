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
	/// <summary>A second action beside the primary one, e.g. "Remove from list"; "" for none.</summary>
	string SecondaryButtonText { get; }
	string CloseButtonText { get; }
	bool CanConfirm { get; }

	/// <summary>Called when the primary button is pressed. Return false to keep the dialog open (e.g. on a validation error).</summary>
	Task<bool> ConfirmAsync();

	/// <summary>Called when the secondary button is pressed, to do its action. Return false to keep the dialog open.</summary>
	Task<bool> SecondaryAsync();
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
		SecondaryButtonText = "";
		CloseButtonText = "Cancel";
		CanConfirm = true;
	}

	[ObservableProperty]
	public partial string Title { get; set; }

	[ObservableProperty]
	public partial string PrimaryButtonText { get; set; }

	[ObservableProperty]
	public partial string SecondaryButtonText { get; set; }

	[ObservableProperty]
	public partial string CloseButtonText { get; set; }

	/// <summary>Enables the primary button; bind validation to this.</summary>
	[ObservableProperty]
	public partial bool CanConfirm { get; set; }

	public TResult? Result { get; protected set; }

	/// <summary>Awaited before the dialog opens, so content that changes its size is there from the start.</summary>
	public virtual Task LoadAsync() => Task.CompletedTask;

	public abstract Task<bool> ConfirmAsync();

	/// <summary>Does the secondary button's action itself: the dialog then returns no <see cref="Result"/>.</summary>
	public virtual Task<bool> SecondaryAsync() => Task.FromResult(true);
}
