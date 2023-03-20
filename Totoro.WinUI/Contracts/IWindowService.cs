namespace Totoro.WinUI.Contracts
{
    public interface IWindowService
    {
        IObservable<bool> IsFullWindowChanged { get; }

        void SetIsFullWindow(bool isFullWindow);
        void ToggleIsFullWindow();
    }
}
