namespace Totoro.Core.Contracts;

public interface IMediaPlayer : IDisposable
{
    void Play();
    void Play(double offsetInSeconds);
    void Pause();
    void SeekTo(TimeSpan ts);
    void Seek(TimeSpan ts, SeekDirection direction);
    IObservable<Unit> Paused { get; }
    IObservable<Unit> Playing { get; }
    IObservable<Unit> PlaybackEnded { get; }
    IObservable<TimeSpan> PositionChanged { get; }
    IObservable<TimeSpan> DurationChanged { get; }
    Task<Unit> SetMedia(VideoStreamModel stream);
    IMediaTransportControls TransportControls { get; }
}

public enum SeekDirection
{
    Forward,
    Backward,
}

