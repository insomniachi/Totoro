using FlyleafLib.MediaPlayer;
using Splat;

namespace Totoro.WinUI.Media.Flyleaf
{
    public sealed class FlyleafMediaPlayerWrapper : IMediaPlayer, IEnableLogger
    {
        private double? _offsetInSeconds;

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
            DurationChanged = MediaPlayer.WhenAnyValue(x => x.Duration).Select(x => new TimeSpan(x));
            PositionChanged = MediaPlayer.WhenAnyPropertyChanged(nameof(MediaPlayer.CurTime)).Select(_ => new TimeSpan(MediaPlayer.CurTime));
            MediaPlayer.OpenCompleted += MediaPlayer_OpenCompleted;
        }

        private void MediaPlayer_OpenCompleted(object sender, OpenCompletedArgs e)
        {
            if(_offsetInSeconds is > 0)
            {
                MediaPlayer.SeekAccurate((int)TimeSpan.FromSeconds(_offsetInSeconds.Value).TotalMilliseconds);
            }
        }

        public ValueTask AddSubtitle(string file)
        {
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            MediaPlayer.Pause();
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
            _offsetInSeconds = offsetInSeconds;
            MediaPlayer.Play();
        }

        public void Seek(TimeSpan ts, SeekDirection direction)
        {
            var currentTime = new TimeSpan(MediaPlayer.CurTime);
            var newTime = direction == SeekDirection.Forward
                ? currentTime + ts
                : currentTime - ts;

            MediaPlayer.SeekAccurate((int)newTime.TotalMilliseconds);
        }

        public void SeekTo(TimeSpan ts)
        {
            MediaPlayer.SeekAccurate((int)ts.TotalMilliseconds);
        }

        public Task<Unit> SetMedia(VideoStreamModel stream)
        {
            this.Log().Debug($"Creating media from {stream.StreamUrl}");
            MediaPlayer.OpenAsync(stream.StreamUrl);
            return Task.FromResult(Unit.Default);
        }

        public void SetPlaybackRate(PlaybackRate rate)
        {
        }
    }
}
