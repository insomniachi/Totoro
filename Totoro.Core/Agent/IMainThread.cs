namespace Totoro.Core.Agent;

/// <summary>
/// Runs work on the app's UI thread, e.g. agent tools, which change state the UI is bound to (the status bar, the
/// user's list...) and may be called from any thread.
/// </summary>
public interface IMainThread
{
	Task<T> InvokeAsync<T>(Func<Task<T>> action);
}
