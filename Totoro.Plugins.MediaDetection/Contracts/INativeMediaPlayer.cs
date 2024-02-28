using System.Diagnostics;
using FlaUI.Core.AutomationElements;

namespace Totoro.Plugins.MediaDetection.Contracts
{
    public interface INativeMediaPlayer : IDisposable
    {
        IObservable<string> TitleChanged { get; }
        Task Initialize(Window window);
        Process? Process { get; }
    }

    public interface IHavePosition
    {
        IObservable<TimeSpan> PositionChanged { get; }
        IObservable<TimeSpan> DurationChanged { get; }
    }

    public interface ICanLaunch
    {
        Task Launch(string title, string url);
    }
}
