using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Navigation;
using Totoro.UI.Networking;

namespace Totoro.UI.Navigation;

public abstract class NavigatableViewModel : ObservableObject, INavigationAware
{
	private int _busyCount;

	/// <summary>
	/// True while the view model is doing work the user should wait for.
	/// Set automatically while <see cref="OnNavigatedToInternalAsync"/> runs; use <see cref="BeginBusy"/>
	/// or <see cref="RunBusyAsync"/> for other work (refresh, search, ...).
	/// Bind it to a toolkit <c>SwitchPresenter</c> (TargetType="x:Boolean") to swap in a spinner.
	/// </summary>
	public bool IsBusy => _busyCount > 0;

	public async Task OnNavigatedFromAsync()
	{
		NetworkStatus.Current.Restored -= OnNetworkRestored;
		await OnNavigatedFromInternalAsync();
	}

	public async Task OnNavigatedToAsync()
	{
		NetworkStatus.Current.Restored += OnNetworkRestored;
		using (BeginBusy())
		{
			await OnNavigatedToInternalAsync();
		}
	}

	protected virtual Task OnNavigatedToInternalAsync() => Task.CompletedTask;
	protected virtual Task OnNavigatedFromInternalAsync() => Task.CompletedTask;

	/// <summary>
	/// Called (on the UI thread, while the page is shown) when the PC is back online: override to load again what
	/// failed while it was offline. Does nothing by default.
	/// </summary>
	protected virtual Task OnNetworkRestoredAsync() => Task.CompletedTask;

	private async void OnNetworkRestored(object? sender, EventArgs e)
	{
		try
		{
			await RunBusyAsync(OnNetworkRestoredAsync);
		}
		catch
		{
			// The page's own retry is still there; a failed automatic one mustn't crash the app.
		}
	}

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
