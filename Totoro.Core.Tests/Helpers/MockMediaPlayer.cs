using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Totoro.Core.Tests.Helpers
{
    internal class MockMediaPlayer : IMediaPlayer
    {
        public Mock<IMediaTransportControls> TransportControlsMock { get; } = new();
        private readonly Subject<Unit> _onDynamicSkip = new();

        public MockMediaPlayer()
        {
            TransportControlsMock.SetupSet(x => x.IsSkipButtonVisible = It.IsAny<bool>()).Verifiable();
            TransportControlsMock.Setup(x => x.OnDynamicSkip).Returns(_onDynamicSkip);
            TransportControlsMock.Setup(x => x.OnStaticSkip).Returns(Observable.Empty<Unit>);
            TransportControlsMock.Setup(x => x.OnNextTrack).Returns(Observable.Empty<Unit>);
            TransportControlsMock.Setup(x => x.OnPrevTrack).Returns(Observable.Empty<Unit>);
        }

        public Subject<Unit> PausedSubject { get; } = new();
        public Subject<Unit> PlayingSubject { get; } = new();
        public Subject<Unit> PlaybackEndedSubject { get; } = new();
        private Subject<TimeSpan> PositionChangedSubject { get; } = new();
        private Subject<TimeSpan> DurationChangedSubject { get; } = new();
        public TimeSpan LastSeekedTime { get; private set; }

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

        public void PressDynamicSkip() => _onDynamicSkip.OnNext(Unit.Default);

        public IObservable<Unit> Paused => PausedSubject;
        public IObservable<Unit> Playing => PlayingSubject;
        public IObservable<Unit> PlaybackEnded => PlaybackEndedSubject;
        public IObservable<TimeSpan> PositionChanged => PositionChangedSubject;
        public IObservable<TimeSpan> DurationChanged => DurationChangedSubject;
        public IMediaTransportControls TransportControls => TransportControlsMock.Object;
        public MediaPlayerType Type => MediaPlayerType.Vlc;

        public void Dispose() { }
        public void Pause() { PausedSubject.OnNext(Unit.Default); }
        public void Play() { PlayingSubject.OnNext(Unit.Default); }
        public void Play(double offsetInSeconds) { PlayingSubject.OnNext(Unit.Default); }
        public void Seek(TimeSpan ts, SeekDirection direction) { LastSeekedTime = ts; }
        public void SeekTo(TimeSpan ts) { LastSeekedTime = ts; }
        public Task<Unit> SetMedia(VideoStreamModel stream) => Task.FromResult(Unit.Default);

        public ValueTask AddSubtitle(string file) => ValueTask.CompletedTask;

        public void SetPlaybackRate(PlaybackRate rate)
        {

        }
    }
}
