using Microsoft.Extensions.Logging;
using Splat;

namespace Totoro.Core
{
    public class DefaultExceptionHandler : IObserver<Exception>, IEnableLogger
    {

        public DefaultExceptionHandler()
        {
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            this.Log().Error(error, "Unhandled Exception");
        }

        public void OnNext(Exception value)
        {

        }
    }
}
