using System.Diagnostics;
using FlaUI.Core.AutomationElements;

namespace Totoro.Plugins.MediaDetection.Contracts
{
    public interface INativeMediaPlayer : IDisposable
    {
        string GetTitle();
        void Initialize(Window window);
        Process? Process { get; }
    }

    public interface IHavePosition
    {
        IObservable<TimeSpan> PositionChanged { get; }
        IObservable<TimeSpan> DurationChanged { get; }
    }
}
