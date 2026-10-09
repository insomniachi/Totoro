using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

public abstract class NavigatableViewModel : ObservableObject, INavigationAware
{
	private int _busyCount;

	/// <summary>
	/// True while the view model is doing work the user should wait for.
	/// Set automatically while <see cref="OnNavigatedToInternal"/> runs; use <see cref="BeginBusy"/>
	/// or <see cref="RunBusyAsync"/> for other work (refresh, search, ...).
	/// Bind it to a toolkit <c>SwitchPresenter</c> (TargetType="x:Boolean") to swap in a spinner.
	/// </summary>
	public bool IsBusy => _busyCount > 0;

	public async Task OnNavigatedFrom()
	{
		await OnNavigatedFromInternal();
	}

	public async Task OnNavigatedTo()
	{
		using (BeginBusy())
		{
			await OnNavigatedToInternal();
		}
	}

	protected virtual Task OnNavigatedToInternal() => Task.CompletedTask;
	protected virtual Task OnNavigatedFromInternal() => Task.CompletedTask;

	/// <summary>
	/// Marks the view model busy until the returned handle is disposed.
	/// Calls can overlap: <see cref="IsBusy"/> stays true until the last one ends.
	/// Call from the UI thread (PropertyChanged is raised on the calling thread).
	/// </summary>
	protected IDisposable BeginBusy()
	{
		if (++_busyCount == 1)
		{
			OnPropertyChanged(nameof(IsBusy));
		}

		return new BusyHandle(this);
	}

	/// <summary>Runs <paramref name="work"/> with <see cref="IsBusy"/> set.</summary>
	protected async Task RunBusyAsync(Func<Task> work)
	{
		using (BeginBusy())
		{
			await work();
		}
	}

	private void EndBusy()
	{
		if (--_busyCount == 0)
		{
			OnPropertyChanged(nameof(IsBusy));
		}
	}

	private sealed class BusyHandle(NavigatableViewModel owner) : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			owner.EndBusy();
		}
	}
}
