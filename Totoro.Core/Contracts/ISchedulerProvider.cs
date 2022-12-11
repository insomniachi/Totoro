using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;

namespace Totoro.Core.Contracts
{
    public interface ISchedulerProvider
    {
        IScheduler MainThreadScheduler { get; }
        IScheduler TaskpoolScheduler { get; }
    }
}
