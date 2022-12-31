using System.Reactive.Concurrency;

namespace Totoro.Core.Contracts
{
    public interface ISchedulerProvider
    {
        IScheduler MainThreadScheduler { get; }
        IScheduler TaskpoolScheduler { get; }
    }
}
