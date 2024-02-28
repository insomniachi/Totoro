using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Flurl;
using Flurl.Http;
using ReactiveUI;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Vlc.HttpInterface
{
    internal sealed class VlcInterface : IDisposable
    {
        private readonly string _api;
        private readonly CompositeDisposable _disposables = new();
        private readonly ReplaySubject<string> _titleChanged = new();
        private readonly ReplaySubject<TimeSpan> _durationChanged = new();
        private readonly ReplaySubject<TimeSpan> _timeChanged = new();

        public IObservable<string> TitleChanged { get; }
        public IObservable<TimeSpan> DurationChanged { get; }
        public IObservable<TimeSpan> PositionChanged { get; }

        public VlcInterface(Process process)
        {
            var host = ConfigManager<Config>.Current.Host;
            var port = ConfigManager<Config>.Current.Port;
            _api = $"http://{host}:{port}";

            Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1))
                .Where(_ => !process.HasExited)
                .SelectMany(_ => GetStatus())
                .WhereNotNull()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(status =>
                {
                    if(!string.IsNullOrEmpty(status!.Information?.Category?.Meta?.FileName))
                    {
                        _titleChanged.OnNext(Path.GetFileNameWithoutExtension(status!.Information.Category.Meta.FileName));
                    }
                    _durationChanged.OnNext(TimeSpan.FromSeconds(status!.Length));
                    _timeChanged.OnNext(TimeSpan.FromSeconds(status!.Time));
                })
                .DisposeWith(_disposables);

            TitleChanged = _titleChanged.DistinctUntilChanged();
            DurationChanged = _durationChanged.DistinctUntilChanged();
            PositionChanged = _timeChanged.DistinctUntilChanged();
        }


        public async Task<VlcStatus?> GetStatus()
        {
            var result = await _api
                .AppendPathSegment("/requests/status.json")
                .WithBasicAuth("", "password").GetAsync();

            if(result.StatusCode >= 300)
            {
                return null;
            }

            return await result.GetJsonAsync<VlcStatus>();
        }

        public async Task SeekTo(TimeSpan timeSpan)
        {
           _ = await _api
                .AppendPathSegment("/requets/status.json")
                .SetQueryParam("command", "seek")
                .SetQueryParam("val", timeSpan.TotalSeconds)
                .WithBasicAuth("", "password")
                .GetAsync();
        }

        public async Task SetVolume(int percent)
        {
            _ = await _api
             .AppendPathSegment("/requets/status.json")
             .SetQueryParam("command", "volume")
             .SetQueryParam("val", $"{percent}%")
             .WithBasicAuth("", "password")
             .GetAsync();
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
