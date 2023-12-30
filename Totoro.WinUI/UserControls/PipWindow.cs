using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Services;
using WinUIEx;

namespace Totoro.WinUI.UserControls
{
    internal sealed class PipWindow() : WindowEx
    {
        private WindowPersistenceService _persistenceService;

        public PipWindow(ILocalSettingsService localSettingsService,
                         IWindowService windowService): this()
        {
            IsMaximizable = false;
            IsMinimizable = false;
            IsResizable = true;
            IsAlwaysOnTop = true;
            IsTitleBarVisible = true;

            var persistence = new WindowPersistenceService.WindowPersistence()
            {
                Size = new Windows.Foundation.Size(570, 400),
                WindowState = Microsoft.UI.Windowing.OverlappedPresenterState.Restored
            };
            _persistenceService = new WindowPersistenceService(localSettingsService, windowService, this, "PipWindow", persistence);
            Closed += (_, _) => _persistenceService.Dispose();
        }

        public void Initalize(UIElement content, string title)
        {
            Title = title;
            WindowContent = new Grid()
            {
                Children = { content }
            };
        }
    }
}
