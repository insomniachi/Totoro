using System.Reactive;

namespace Totoro.Plugins.MediaDetection.Contracts
{
    public interface INativeMediaPlayer
    {
        string GetTitle();
        void Initialize(string fileName);
        void Initialize(int processId);
        IObservable<TimeSpan> PositionChanged { get; }
        IObservable<TimeSpan> DurationChanged { get; }
        int ProcessId { get; }
    }
}
