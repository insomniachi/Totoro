using System.Reactive.Concurrency;

namespace Totoro.Core.Services;

public class SchedulerProvider : ISchedulerProvider
{
    public IScheduler MainThreadScheduler => RxApp.MainThreadScheduler;

    public IScheduler TaskpoolScheduler => RxApp.TaskpoolScheduler;
}
