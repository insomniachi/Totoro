using Totoro.WinUI.Contracts;

namespace Totoro.WinUI.Services
{
    internal class WindowService : IWindowService
    {
        private bool _isFullWindow = false;
        private readonly ScheduledSubject<bool> _isFullWindowChanged = new(RxApp.MainThreadScheduler);
        public IObservable<bool> IsFullWindowChanged => _isFullWindowChanged;
        public void SetIsFullWindow(bool isFullWindow)
        {
            if(_isFullWindow == isFullWindow)
            {
                return;
            }

            _isFullWindow = isFullWindow;
            _isFullWindowChanged.OnNext(isFullWindow);
        }

        public void ToggleIsFullWindow()
        {
            _isFullWindow = !_isFullWindow;
            _isFullWindowChanged.OnNext(_isFullWindow);
        }
    }
}
