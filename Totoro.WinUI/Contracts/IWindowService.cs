namespace Totoro.WinUI.Contracts
{
    public interface IWindowService
    {
        public bool IsFullWindow { get; set; }
        IObservable<bool> IsFullWindowChanged { get; }

        void SetIsFullWindow(bool isFullWindow);
        void ToggleIsFullWindow();
    }
}
