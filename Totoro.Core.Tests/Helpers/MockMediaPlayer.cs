using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Totoro.Core.Tests.Helpers
{
    internal class MockMediaPlayer : IMediaPlayer
    {
        private readonly Mock<IMediaTransportControls> _tranport = new();

        public MockMediaPlayer()
        {
            _tranport.Setup(x => x.OnDynamicSkip).Returns(Observable.Empty<Unit>);
            _tranport.Setup(x => x.OnStaticSkip).Returns(Observable.Empty<Unit>);
            _tranport.Setup(x => x.OnNextTrack).Returns(Observable.Empty<Unit>);
            _tranport.Setup(x => x.OnPrevTrack).Returns(Observable.Empty<Unit>);
        }

        public Subject<Unit> PausedSubject { get; } = new();
        public Subject<Unit> PlayingSubject { get; } = new();
        public Subject<Unit> PlaybackEndedSubject { get; } = new();
        private Subject<TimeSpan> PositionChangedSubject { get; } = new();
        private Subject<TimeSpan> DurationChangedSubject { get; } = new();
        public void ConfigureTransportControls(Action<Mock<IMediaTransportControls>> configure)
        {
            configure(_tranport);
        }

        public async Task SetDuration(TimeSpan ts)
        {
            DurationChangedSubject.OnNext(ts);
            await Task.Delay(1100);
        }

        public async Task SetPosition(TimeSpan ts)
        {
            PositionChangedSubject.OnNext(ts);
            await Task.Delay(10);
        }

        public IObservable<Unit> Paused => PausedSubject;
        public IObservable<Unit> Playing => PlayingSubject;
        public IObservable<Unit> PlaybackEnded => PlaybackEndedSubject;
        public IObservable<TimeSpan> PositionChanged => PositionChangedSubject;
        public IObservable<TimeSpan> DurationChanged => DurationChangedSubject;
        public IMediaTransportControls TransportControls => _tranport.Object;
        public MediaPlayerType Type => MediaPlayerType.Vlc;

        public void Dispose() { }
        public void Pause() { }
        public void Play() { }
        public void Play(double offsetInSeconds) { }
        public void Seek(TimeSpan ts, SeekDirection direction) { }
        public void SeekTo(TimeSpan ts) { }
        public Task<Unit> SetMedia(VideoStreamModel stream) => Task.FromResult(Unit.Default);
    }
}
