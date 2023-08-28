using Microsoft.UI.Windowing;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;

namespace Totoro.WinUI.Services;

internal sealed class WindowPersistenceService : IDisposable
{
    private readonly WindowEx _window;
    private readonly WindowPersistence _persistence;

    class WindowPersistence : ReactiveObject
    {
        [Reactive] public Size Size { get; set; }
        [Reactive] public Point Position { get; set; }
        [Reactive] public OverlappedPresenterState WindowState { get; set; }
    }

    public WindowPersistenceService(ILocalSettingsService localSettingsService,
                                    WindowEx window,
                                    string name)
    {
        _window = window;
        _persistence = localSettingsService.ReadSetting($"Persistence{name}", new WindowPersistence());

        _window.SizeChanged += SizeChanged;
        _window.PositionChanged += PositionChanged;
        Initialize();

        _persistence.WhenAnyPropertyChanged()
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Subscribe(_ => localSettingsService.SaveSetting($"Persistence{name}", _persistence));
    }

    private void Initialize()
    {
        if(_persistence.WindowState is OverlappedPresenterState.Maximized || _persistence.Size is { Height : 0, Width: 0 } || _persistence.Position is { X : 0, Y: 0 })
        {
            _window.Maximize();
            return;
        }
        var scale = HwndExtensions.GetDpiForWindow(_window.GetWindowHandle()) / 96f;
        _window.MoveAndResize(_persistence.Position.X, _persistence.Position.Y, _persistence.Size.Width / scale, _persistence.Size.Height / scale);
    }

    private void PositionChanged(object sender, PointInt32 e)
    {
        _persistence.Position = new(e.X,e.Y);
    }


    private void SizeChanged(object sender, Microsoft.UI.Xaml.WindowSizeChangedEventArgs args)
    {
        _persistence.Size = args.Size;
        if(_window.Presenter is OverlappedPresenter op)
        {
            _persistence.WindowState = op.State;
        }
    }

    public void Dispose()
    {
        _window.SizeChanged -= SizeChanged;
        _window.PositionChanged -= PositionChanged;
    }
}
