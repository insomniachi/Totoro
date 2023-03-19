namespace Totoro.Core.Contracts;

public interface IMediaPlayer : IDisposable
{
    void Play();
    void Play(double offsetInSeconds);
    void Pause();
    void Seek(TimeSpan ts);
    IObservable<Unit> Paused { get; }
    IObservable<Unit> Playing { get; }
    IObservable<Unit> PlaybackEnded { get; }
    IObservable<TimeSpan> PositionChanged { get; }
    IObservable<TimeSpan> DurationChanged { get; }
    IObservable<Unit> OnDynamicSkip { get; }
    IObservable<Unit> OnStaticSkip { get; }
    Task<Unit> SetMedia(VideoStream stream, Dictionary<string, string> AdditionalInformation);
    Task<Unit> SetMediaFromFile(string localFile);
    ValueTask SetFFMpegMedia(string url);
    Task<Unit> SetMedia(VideoStreamModel stream, Dictionary<string, string> AdditionalInformation);
    bool IsSkipButtonVisible { get; set; }
}
