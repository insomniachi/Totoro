using FlyleafLib.MediaPlayer;
using Splat;

namespace Totoro.WinUI.Media.Flyleaf
{
    public class FlyleafMediaPlayerWrapper : IMediaPlayer, IEnableLogger
    {
        public Player MediaPlayer { get; set; } = new();

        public IObservable<Unit> Paused { get; }

        public IObservable<Unit> Playing { get; }

        public IObservable<Unit> PlaybackEnded { get; }

        public IObservable<TimeSpan> PositionChanged { get; }

        public IObservable<TimeSpan> DurationChanged { get; }

        public IMediaTransportControls TransportControls { get; private set; }

        public MediaPlayerType Type => MediaPlayerType.FFMpeg;

        public void SetTransportControls(IMediaTransportControls transportControls)
        {
            TransportControls = transportControls;
        }

        public FlyleafMediaPlayerWrapper()
        {
            Paused = MediaPlayer.WhenAnyValue(x => x.Status).Where(x => x is Status.Paused).Select(x => Unit.Default);
            Playing = MediaPlayer.WhenAnyValue(x => x.Status).Where(x => x is Status.Playing).Select(x => Unit.Default);
            PlaybackEnded = MediaPlayer.WhenAnyValue(x => x.Status).Where(x => x is Status.Ended).Select(x => Unit.Default);
            DurationChanged = MediaPlayer.WhenAnyValue(x => x.Duration).Select(x => TimeSpan.FromMilliseconds(x));
            PositionChanged = MediaPlayer.WhenAnyPropertyChanged(nameof(MediaPlayer.CurTime)).Select(_ => TimeSpan.FromMilliseconds(MediaPlayer.CurTime));
        }

        public ValueTask AddSubtitle(string file)
        {
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
        }

        public void Pause()
        {
            MediaPlayer.Pause();
        }

        public void Play()
        {
            MediaPlayer.Play();
        }

        public void Play(double offsetInSeconds)
        {
            MediaPlayer.Play();
        }

        public void Seek(TimeSpan ts, SeekDirection direction)
        {
            MediaPlayer.Seek((int)ts.TotalMilliseconds, direction == SeekDirection.Forward);
        }

        public void SeekTo(TimeSpan ts)
        {
        }

        public Task<Unit> SetMedia(VideoStreamModel stream)
        {
            this.Log().Debug($"Creating media from {stream.StreamUrl}");
            MediaPlayer.OpenAsync(stream.StreamUrl);
            return Task.FromResult(Unit.Default);
        }
    }
}
