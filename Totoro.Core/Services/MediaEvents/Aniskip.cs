using System.Reactive.Concurrency;

namespace Totoro.Core.Services.MediaEvents;


internal interface IAniskip
{
    Task SubmitTimeStamp();
}

internal class Aniskip : MediaEventListener, IAniskip
{
    private readonly IViewService _viewService;
    private readonly ISettings _settings;
    private TimeSpan? _duration;
    private TimeSpan _position;
    private AniSkipResultItem _op;
    private AniSkipResultItem _ed;
    private double _staticSkipPosition;
    private TimeSpan _endTime = TimeSpan.MaxValue;
    private bool _isDialogShown;

    public Aniskip(IViewService viewService,
                   ISettings settings)
    {
        _viewService = viewService;
        _settings = settings;
    }

    protected override void OnPositionChanged(TimeSpan position)
    {
        _position = position;

        if (_timeStamps is not null)
        {
            RxApp.MainThreadScheduler.Schedule(() => _mediaPlayer.TransportControls.IsSkipButtonVisible = IsOpeningOrEnding(position.TotalSeconds));
        }

        if (CanSubmitTimeStamp())
        {
            _isDialogShown = true;
            RxApp.MainThreadScheduler.Schedule(async () =>
            {
                await SubmitTimeStamp();
            });
        }
    }

    private bool CanSubmitTimeStamp() => _op is null &&
                                         _position > _endTime &&
                                         _isDialogShown == false &&
                                         _duration is not null &&
                                         _animeModel is not null;

    protected override void OnDurationChanged(TimeSpan duration)
    {
        _duration = duration;
        _endTime = duration - TimeSpan.FromSeconds(_settings.TimeRemainingWhenEpisodeCompletesInSeconds);
    }

    protected override void OnEpisodeChanged()
    {
        _duration = null;
        _timeStamps = null;
        _op = null;
        _ed = null;
        _staticSkipPosition = 0;
        _isDialogShown = false;
    }

    protected override void OnDynamicSkipped()
    {
        var position = _position.TotalSeconds;
        if (_ed is not null && position > _ed.Interval.StartTime)
        {
            _mediaPlayer.SeekTo(TimeSpan.FromSeconds(_ed.Interval.EndTime));
        }
        else if (_op is not null && position > _op.Interval.StartTime)
        {
            _mediaPlayer.SeekTo(TimeSpan.FromSeconds(_op.Interval.EndTime));
        }
    }

    protected override void OnStaticSkipped()
    {
        _staticSkipPosition = _position.TotalSeconds;
    }

    protected override void OnTimestampsChanged()
    {
        _op = _timeStamps.Opening;
        _ed = _timeStamps.Ending;
    }

    private bool IsOpeningOrEnding(double position)
    {
        var isOpening = _op is not null && position > _op.Interval.StartTime && position < _op.Interval.EndTime;
        var isEnding = _ed is not null && position > _ed.Interval.StartTime && position < _ed.Interval.EndTime;

        return isOpening || isEnding;
    }

    public async Task SubmitTimeStamp()
    {
        _mediaPlayer.Pause();
        await _viewService.SubmitTimeStamp(_animeModel.Id, _currentEpisode, _videoStreamModel, _timeStamps, _duration.Value.TotalSeconds, _staticSkipPosition);
        _mediaPlayer.Play();
    }
}
