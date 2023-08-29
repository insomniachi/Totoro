using Totoro.WinUI.Contracts;

namespace Totoro.WinUI.Services
{
    internal class WindowService : IWindowService
    {
        public bool IsFullWindow { get; set; }

        private readonly ScheduledSubject<bool> _isFullWindowChanged = new(RxApp.MainThreadScheduler);
        public IObservable<bool> IsFullWindowChanged => _isFullWindowChanged;
        public void SetIsFullWindow(bool isFullWindow)
        {
            if (IsFullWindow == isFullWindow)
            {
                return;
            }

            IsFullWindow = isFullWindow;
            _isFullWindowChanged.OnNext(isFullWindow);
        }

        public void ToggleIsFullWindow()
        {
            IsFullWindow = !IsFullWindow;
            _isFullWindowChanged.OnNext(IsFullWindow);
        }
    }
}
