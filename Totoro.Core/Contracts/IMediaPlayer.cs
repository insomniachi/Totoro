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
    Task<Unit> SetFFMpegMedia(VideoStreamModel stream);
    Task<Unit> SetMedia(VideoStreamModel stream, Dictionary<string, string> AdditionalInformation);
    IMediaTransportControls TransportControls { get; }
}

public interface IMediaTransportControls
{
    IObservable<Unit> OnNextTrack { get; }
    IObservable<Unit> OnPrevTrack { get; }
    IObservable<Unit> OnStaticSkip { get; }
    IObservable<Unit> OnDynamicSkip { get; }
    IObservable<Unit> OnAddCc { get; }
    IObservable<string> OnQualityChanged { get; }
    IObservable<Unit> OnSubmitTimeStamp { get; }
    bool IsSkipButtonVisible { get; set; }
    bool IsNextTrackButtonVisible { get; set; }
    bool IsPreviousTrackButtonVisible { get; set; }
}

