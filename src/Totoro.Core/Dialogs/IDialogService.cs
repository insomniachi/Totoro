namespace Totoro.Core.Dialogs;

public interface IDialogService
{
	/// <summary>
	/// Shows the dialog registered for <typeparamref name="TViewModel"/> with <c>AddDialog</c>.
	/// The view model is created from DI; <paramref name="parameter"/>, if given, is passed to its constructor.
	/// </summary>
	/// <returns>The view model's <see cref="DialogViewModel{TResult}.Result"/> if the dialog was confirmed, otherwise default.</returns>
	Task<TResult?> ShowAsync<TViewModel, TResult>(object? parameter = null)
		where TViewModel : DialogViewModel<TResult>;
}
