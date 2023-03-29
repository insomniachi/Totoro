using System.Reactive.Concurrency;

namespace Totoro.WinUI.Media.Vlc;

/// <summary>
/// Dispatcher adapter
/// </summary>
internal class DispatcherAdapter : IDispatcher
{
    /// <summary>
    /// Initializes a new instance 
    /// </summary>
    /// <param name="dispatcher"></param>
    public DispatcherAdapter()
    {
    }


    /// <summary>
    /// Schedules the provided callback on the UI thread from a worker threa
    /// </summary>
    /// <param name="action">The callback on which the dispatcher returns when the event is dispatched</param>
    /// <returns>The task object representing the asynchronous operation</returns>
    public Task InvokeAsync(Action action)
    {
        RxApp.MainThreadScheduler.Schedule(action);
        return Task.CompletedTask;
    }
}
