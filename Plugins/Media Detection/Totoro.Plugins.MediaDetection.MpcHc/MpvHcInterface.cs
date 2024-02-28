using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using Flurl;
using Flurl.Http;
using ReactiveUI;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.MpcHc;

internal sealed partial class MpvHcInterface : IDisposable
{
    private readonly string _api;
    private readonly CompositeDisposable _disposables = new();
    private readonly ReplaySubject<string> _titleChanged = new();
    private readonly ReplaySubject<TimeSpan> _durationChanged = new();
    private readonly ReplaySubject<TimeSpan> _timeChanged = new();

    [GeneratedRegex(@"<p id=""(?<Variable>(file|positionstring|durationstring))"">(?<Value>[^<>]*)")]
    private partial Regex VariablesRegex();

    public IObservable<string> TitleChanged { get; }
    public IObservable<TimeSpan> DurationChanged { get; }
    public IObservable<TimeSpan> PositionChanged { get; }

    public MpvHcInterface(Process process)
    {
        var host = ConfigManager<Config>.Current.Host;
        var port = ConfigManager<Config>.Current.Port;
        _api = $"http://{host}:{port}";

        Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1))
            .Where(_ => !process.HasExited)
            .SelectMany(_ => GetVariables())
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(variables =>
            {
                if (!string.IsNullOrEmpty(variables.Title))
                {
                    _titleChanged.OnNext(variables.Title);
                }
                _durationChanged.OnNext(variables.Duration);
                _timeChanged.OnNext(variables.Time);
            })
            .DisposeWith(_disposables);

        TitleChanged = _titleChanged.DistinctUntilChanged();
        DurationChanged = _durationChanged.DistinctUntilChanged();
        PositionChanged = _timeChanged.DistinctUntilChanged();
    }

    private async Task<Variables?> GetVariables()
    {
        var result = await _api.AppendPathSegment("variables.html").GetAsync();

        if(result.StatusCode >= 300)
        {
            return null;
        }

        var html = await result.GetStringAsync();
        var variables = new Variables();

        foreach (var match in VariablesRegex().Matches(html).OfType<Match>().Where(x => x.Success))
        {
            var name = match.Groups["Variable"].Value;
            var value = match.Groups["Value"].Value;

            if (name == "file")
            {
                variables.Title = Path.GetFileNameWithoutExtension(value);
            }
            else if(name == "positionstring")
            {
                variables.Time = TimeSpan.Parse(value);
            }
            else if(name == "durationstring")
            {
                variables.Duration = TimeSpan.Parse(value);
            }
        }

        return variables;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

}

internal class Variables
{
    public string Title { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public TimeSpan Time { get; set; }
}
