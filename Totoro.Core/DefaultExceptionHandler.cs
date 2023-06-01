using Microsoft.Extensions.Logging;
using Splat;

namespace Totoro.Core
{
    public class DefaultExceptionHandler : IObserver<Exception>, IEnableLogger
    {
        private readonly IViewService _viewService;

        public DefaultExceptionHandler(IViewService viewService)
        {
            _viewService = viewService;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            this.Log().Error(error, "Unhandled Exception");
            _viewService.UnhandledException(error);
        }

        public void OnNext(Exception value)
        {

        }
    }
}
