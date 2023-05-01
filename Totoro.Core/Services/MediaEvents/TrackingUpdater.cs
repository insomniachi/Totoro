namespace Totoro.Core.Services.MediaEvents;

public interface ITrackingUpdater
{
    public event EventHandler TrackingUpdated;
}

internal class TrackingUpdater : MediaEventListener, ITrackingUpdater
{
    private readonly ITrackingServiceContext _trackingService;
    private readonly ISettings _settings;
    private readonly IViewService _viewService;
    private readonly ISystemClock _systemClock;
    private TimeSpan _updateAt = TimeSpan.MaxValue;
    private TimeSpan _position;
    private TimeSpan _duration;
    private static readonly TimeSpan _nextBuffer = TimeSpan.FromMinutes(3);
    private bool _isUpdated;

    public event EventHandler TrackingUpdated;

    public TrackingUpdater(ITrackingServiceContext trackingService,
                           ISettings settings,
                           IViewService viewService,
                           ISystemClock systemClock)
    {
        _trackingService = trackingService;
        _settings = settings;
        _viewService = viewService;
        _systemClock = systemClock;
    }

    protected override void OnDurationChanged(TimeSpan duration)
    {
        _duration = duration;
        _updateAt = duration - TimeSpan.FromSeconds(_settings.TimeRemainingWhenEpisodeCompletesInSeconds);
    }

    protected override void OnTimestampsChanged()
    {
        if (_timeStamps.Ending is not { } ed)
        {
            return;
        }

        _updateAt = TimeSpan.FromSeconds(ed.Interval.StartTime);
    }

    protected override void OnPositionChanged(TimeSpan position)
    {
        _position = position;
        if (!IsEnabled || position < _updateAt || _isUpdated || _animeModel.Tracking?.WatchedEpisodes >= _currentEpisode)
        {
            return;
        }

        _ = UpdateTracking();
    }

    protected override void OnNextTrack()
    {
        // if less than 3 minutes remaining when clicking next episode, update tracking
        if (_duration - _position > _nextBuffer || _isUpdated)
        {
            return;
        }

        _ = UpdateTracking();
    }

    protected override void OnEpisodeChanged()
    {
        _isUpdated = false;
        _updateAt = TimeSpan.MaxValue;
    }

    private async Task UpdateTracking()
    {
        _isUpdated = true;
        var tracking = new Tracking() { WatchedEpisodes = _currentEpisode };
        if (_currentEpisode == _animeModel.TotalEpisodes)
        {
            tracking.Status = AnimeStatus.Completed;
            tracking.FinishDate = _systemClock.Today;

            if (_animeModel?.Tracking?.Score is null && await _viewService.RequestRating(_animeModel) is { } score && score > 0)
            {
                tracking.Score = score;
            }

        }
        else if (_currentEpisode == 1)
        {
            tracking.Status = AnimeStatus.Watching;
            tracking.StartDate = _systemClock.Today;
        }

        _animeModel.Tracking = await _trackingService.Update(_animeModel.Id, tracking);
        TrackingUpdated?.Invoke(this, EventArgs.Empty);
    }

    public bool IsEnabled => _animeModel is not null;
}

