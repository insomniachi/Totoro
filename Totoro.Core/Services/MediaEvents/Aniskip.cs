namespace Totoro.Core.Services.MediaEvents;


internal interface IAniskip
{
    Task SubmitTimeStamp();
}

internal class Aniskip : MediaEventListener, IAniskip
{
    private readonly ITimestampsService _timestampsService;
    private readonly IViewService _viewService;
    private TimeSpan? _duration;
    private TimeSpan _position;
    private AniSkipResult _skipResult;
    private AniSkipResultItem _op;
    private AniSkipResultItem _ed;
    private double _staticSkipPosition;

    public Aniskip(ITimestampsService timestampsService,
                   IViewService viewService)
    {
        _timestampsService = timestampsService;
        _viewService = viewService;
    }

    protected override void OnDurationChanged(TimeSpan duration)
    {
        if(_duration is not null)
        {
            return;
        }

        if(_animeModel is null || _currentEpisode is 0)
        {
            return;
        }

        _duration = duration;
        _timestampsService
            .GetTimeStamps(_animeModel.Id, _currentEpisode, _duration.Value.TotalSeconds)
            .ToObservable()
            .Subscribe(timeStamp =>
            {
                _skipResult = timeStamp;
                _op = timeStamp.Opening;
                _ed = timeStamp.Ending;
            });
    }

    protected override void OnPositionChanged(TimeSpan position)
    {
        _position = position;
        _mediaPlayer.IsSkipButtonVisible = IsOpeningOrEnding(position.TotalSeconds);
    }

    protected override void OnEpisodeChanged()
    {
        _duration = null;
        _staticSkipPosition = 0;
        _op = null;
        _ed = null;
        _skipResult = null;
    }

    protected override void OnDynamicSkipped()
    {
        var position = _position.TotalSeconds;
        if (_ed is not null && position > _ed.Interval.StartTime)
        {
            _mediaPlayer.Seek(TimeSpan.FromSeconds(_ed.Interval.EndTime));
        }
        else if (_op is not null && position > _op.Interval.StartTime)
        {
            _mediaPlayer.Seek(TimeSpan.FromSeconds(_op.Interval.EndTime));
        }
    }

    protected override void OnStaticSkipped()
    {
        _staticSkipPosition = _position.TotalSeconds;
    }

    private bool IsOpeningOrEnding(double position)
    {
        var isOpening = _op is not null && position > _op.Interval.StartTime && position < _op.Interval.EndTime;
        var isEnding = _ed is not null && position > _ed.Interval.StartTime && position < _ed.Interval.EndTime;

        return isOpening || isEnding;
    }

    public async Task SubmitTimeStamp()
    {
        await _viewService.SubmitTimeStamp(_animeModel.Id, _currentEpisode, _videoStreamModel, _skipResult, _duration.Value.TotalSeconds, _staticSkipPosition);
    }
}
