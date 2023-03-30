using System.Reactive;
using System.Reactive.Subjects;

namespace Totoro.Core.Tests.Helpers
{
    internal class MockMediaPlayer : IMediaPlayer
    {
        public Subject<Unit> PausedSubject { get; } = new();
        public Subject<Unit> PlayingSubject { get; } = new();
        public Subject<Unit> PlaybackEndedSubject { get; } = new();
        public Subject<TimeSpan> PositionChangedSubject { get; } = new();
        public Subject<TimeSpan> DurationChangedSubject { get; } = new();

        public IObservable<Unit> Paused => PausedSubject;
        public IObservable<Unit> Playing => PlayingSubject;
        public IObservable<Unit> PlaybackEnded => PlaybackEndedSubject;
        public IObservable<TimeSpan> PositionChanged => PositionChangedSubject;
        public IObservable<TimeSpan> DurationChanged => DurationChangedSubject;
        public IMediaTransportControls TransportControls => throw new NotImplementedException();

        public void Dispose() { }
        public void Pause() { }
        public void Play() { }
        public void Play(double offsetInSeconds) { }
        public void SeekTo(TimeSpan ts) { }
        public Task<Unit> SetFFMpegMedia(string url) => Task.FromResult(Unit.Default);
        public Task<Unit> SetMedia(VideoStreamModel stream, Dictionary<string, string> AdditionalInformation) => Task.FromResult(Unit.Default);
    }
}
