using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Totoro.Core.Agent;

namespace Totoro.UI.Threading;

/// <summary>Runs work on the UI thread of the window <paramref name="dispatcher"/> gives, once there is one.</summary>
public sealed class MainThread(Func<DispatcherQueue?> dispatcher) : IMainThread
{
	public Task<T> InvokeAsync<T>(Func<Task<T>> action)
	{
		var queue = dispatcher() ?? throw new InvalidOperationException("The app's window isn't open yet.");
		// Runs straight away when already on the UI thread.
		return queue.EnqueueAsync(action);
	}
}
