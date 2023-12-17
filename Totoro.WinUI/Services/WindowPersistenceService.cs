using Microsoft.UI.Windowing;
using Totoro.WinUI.Contracts;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;

namespace Totoro.WinUI.Services;

internal sealed class WindowPersistenceService : IDisposable
{
    private readonly WindowEx _window;
    private readonly WindowPersistence _persistence;
    private readonly IWindowService _windowService;

    class WindowPersistence : ReactiveObject
    {
        [Reactive] public Size Size { get; set; }
        [Reactive] public Point Position { get; set; }
        [Reactive] public OverlappedPresenterState WindowState { get; set; } = OverlappedPresenterState.Maximized;
    }

    public WindowPersistenceService(ILocalSettingsService localSettingsService,
                                    IWindowService windowService,
                                    WindowEx window,
                                    string name)
    {
        _window = window;
        _windowService = windowService;
        _persistence = localSettingsService.ReadSetting($"Persistence{name}", new WindowPersistence());

        _window.SizeChanged += SizeChanged;
        _window.PositionChanged += PositionChanged;
        _window.PresenterChanged += PresenterChanged;

        Initialize();

        _persistence.WhenAnyPropertyChanged()
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Subscribe(_ => localSettingsService.SaveSetting($"Persistence{name}", _persistence));
    }

    private void PresenterChanged(object sender, AppWindowPresenter e)
    {
        if (_window.Presenter is not OverlappedPresenter op)
        {
            return;
        }

        _persistence.WindowState = op.State;
    }

    private void Initialize()
    {
        if (_persistence.WindowState is OverlappedPresenterState.Maximized)
        {
            _window.Maximize();
            return;
        }
        var scale = HwndExtensions.GetDpiForWindow(_window.GetWindowHandle()) / 96f;
        _window.MoveAndResize(_persistence.Position.X, _persistence.Position.Y, _persistence.Size.Width / scale, _persistence.Size.Height / scale);
    }

    private void PositionChanged(object sender, PointInt32 e)
    {
        if (_window.PresenterKind != AppWindowPresenterKind.Overlapped || _windowService.IsFullWindow)
        {
            return;
        }

        if(e.X < 0 || e.Y < 0)
        {
            return;
        }

        _persistence.Position = new(e.X, e.Y);
    }


    private void SizeChanged(object sender, Microsoft.UI.Xaml.WindowSizeChangedEventArgs args)
    {
        if (_window.Presenter is not OverlappedPresenter op || _windowService.IsFullWindow)
        {
            return;
        }

        if (args.Size.Height < 0 || args.Size.Width < 0)
        {
            return;
        }

        _persistence.Size = args.Size;
        _persistence.WindowState = op.State;
    }

    public void Dispose()
    {
        _window.SizeChanged -= SizeChanged;
        _window.PositionChanged -= PositionChanged;
        _window.PresenterChanged -= PresenterChanged;
    }
}
