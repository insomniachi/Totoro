using System.Diagnostics;
using System.Reactive.Subjects;
using FlaUI.Core.AutomationElements;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection.Win11MediaPlayer;

internal partial class MediaPlayer : INativeMediaPlayer
{
    private readonly Subject<TimeSpan> _positionChanged = new();
    private readonly Subject<TimeSpan> _durationChanged = new();
    private Window? _mainWindow;


    public IObservable<TimeSpan> PositionChanged => _positionChanged;
    public IObservable<TimeSpan> DurationChanged => _durationChanged;
    public Process? Process { get; private set; }
    public TimeSpan Duration { get; set; }

    public void Dispose() { }

    public string GetTitle()
    {
        if (_mainWindow is null)
        {
            return string.Empty;
        }

        var element = _mainWindow.FindFirstDescendant(cb => cb.ByAutomationId("mediaItemTitle"));
        while (element is null)
        {
            element = _mainWindow.FindFirstDescendant(cb => cb.ByAutomationId("mediaItemTitle"));
        }

        return element.Name;
    }


    public void Initialize(Window window)
    {
        _mainWindow = window;
        Process = Process.GetProcessesByName("Microsoft.Media.Player").First();
    }
}
