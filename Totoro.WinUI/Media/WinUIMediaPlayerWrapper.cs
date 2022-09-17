using ReactiveMarbles.ObservableEvents;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Totoro.WinUI.Media;

public sealed class WinUIMediaPlayerWrapper : IMediaPlayer
{
    private readonly MediaPlayer _player = new();
    

    public IObservable<Unit> Paused => _player.Events().CurrentStateChanged.Where(x => x.sender.CurrentState == MediaPlayerState.Paused).Select(_ => Unit.Default);

    public IObservable<Unit> Playing => _player.Events().CurrentStateChanged.Where(x => x.sender.CurrentState == MediaPlayerState.Playing).Select(_ => Unit.Default);

    public IObservable<Unit> PlaybackEnded => _player.Events().MediaEnded.Select(_ => Unit.Default);

    public IObservable<TimeSpan> PositionChanged => _player.PlaybackSession.Events().PositionChanged.Select(x => x.sender.Position);

    public IObservable<TimeSpan> DurationChanged => _player.PlaybackSession.Events().NaturalDurationChanged.Select(x => x.sender.NaturalDuration);

    public void Dispose() => _player.Pause();

    public void Pause() => _player.Pause();

    public void Play() => _player.Play();

    public void Seek(TimeSpan ts) => _player.Position = ts;

    public void Play(double offsetInSeconds)
    {
        _player.Position = TimeSpan.FromSeconds(offsetInSeconds);
        _player.Play();
    }

    public void SetMedia(VideoStream stream)
    {
        if (stream.Headers is { Count: > 0 } headers)
        {
            // TODO : add request headers
        }
        else
        {
            _player.Source = MediaSource.CreateFromUri(new Uri(stream.Url));
        }
    }

    public MediaPlayer GetMediaPlayer() => _player;
}
