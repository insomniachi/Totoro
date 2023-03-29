using System.Reactive.Subjects;
using ReactiveMarbles.ObservableEvents;
using LibVLCSharp.Shared;
using Totoro.WinUI.Contracts;

namespace Totoro.WinUI.Media;

internal class LibVLCMediaPlayerWrapper : IMediaPlayer
{
    private LibVLC _vlc;
    private MediaPlayer _mp; 
    private readonly Subject<Unit> _paused = new();
    private readonly Subject<Unit> _playing = new();
    private readonly Subject<Unit> _ended = new();
    private readonly Subject<TimeSpan> _durationChanged = new();
    private readonly Subject<TimeSpan> _positionChanged = new();

    public LibVLCMediaPlayerWrapper(IWindowService windowService)
    {
        TransportControls = new CustomMediaTransportControls(windowService);
    }

    public IObservable<Unit> Paused => _paused;

    public IObservable<Unit> Playing => _playing;

    public IObservable<Unit> PlaybackEnded => _ended;

    public IObservable<TimeSpan> PositionChanged => _positionChanged;

    public IObservable<TimeSpan> DurationChanged => _durationChanged;

    public IMediaTransportControls TransportControls { get; private set; }

    public bool IsInitialized { get; private set; }

    public void Dispose()
    {
        _mp?.Dispose();
    }

    public void Pause()
    {
        _mp.Pause();
    }

    public void Play()
    {
        _mp.Play();
    }

    public void Play(double offsetInSeconds)
    {
        _mp.SeekTo(TimeSpan.FromSeconds(offsetInSeconds));
        _mp.Play();
    }

    public void Seek(TimeSpan ts)
    {
        _mp.Time = (long)ts.TotalMilliseconds;
    }

    public Task<Unit> SetMedia(VideoStreamModel stream)
    {
        var media = stream.Stream is null
            ? new LibVLCSharp.Shared.Media(_vlc, stream.StreamUrl, FromType.FromLocation)
            : new LibVLCSharp.Shared.Media(_vlc, new StreamMediaInput(stream.Stream));

        _mp.Media = media;

        return Task.FromResult(Unit.Default);
    }

    public void SetTransportControls(IMediaTransportControls transportControls)
    {
        TransportControls = transportControls;
    }

    public void Initialize(LibVLC vlc, MediaPlayer mp)
    {
        _vlc = vlc;
        _mp = mp;

        _mp.Events()
           .Paused
           .Subscribe(_ => _paused.OnNext(Unit.Default));

        _mp.Events()
           .Playing
           .Subscribe(_ => _playing.OnNext(Unit.Default));

        _mp.Events()
           .TimeChanged
           .Select(x => TimeSpan.FromMilliseconds(x.Time))
           .Subscribe(_positionChanged.OnNext);

        _mp.Events()
           .LengthChanged
           .Where(x => x.Length > 0)
           .Select(x => TimeSpan.FromMilliseconds(x.Length))
           .Subscribe(_durationChanged.OnNext);

        _mp.Events()
           .EndReached
           .Subscribe(_ => _ended.OnNext(Unit.Default));

        IsInitialized = true;
    }


}
