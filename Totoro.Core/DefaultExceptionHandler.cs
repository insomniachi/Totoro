using Microsoft.Extensions.Logging;

namespace Totoro.Core
{
    public class DefaultExceptionHandler : IObserver<Exception>
    {
        private readonly ILogger<DefaultExceptionHandler> _logger;

        public DefaultExceptionHandler(ILogger<DefaultExceptionHandler> logger)
        {
            _logger = logger;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            _logger.LogError(error, "Unhandled Exception");
        }

        public void OnNext(Exception value)
        {

        }
    }
}
