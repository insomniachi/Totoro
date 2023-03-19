namespace Totoro.Core.Services.MediaEvents;

internal class TrackingUpdater : MediaEventListener
{
    private readonly ITrackingServiceContext _trackingService;
    private readonly ISettings _settings;
    private TimeSpan _updateAt;
    private bool _isUpdated;

    public TrackingUpdater(ITrackingServiceContext trackingService,
                           ISettings settings)
    {
        _trackingService = trackingService;
        _settings = settings;
    }

    protected override void OnDurationChanged(TimeSpan duration)
    {
        _updateAt = duration - TimeSpan.FromSeconds(_settings.TimeRemainingWhenEpisodeCompletesInSeconds);
    }

    protected override void OnPositionChanged(TimeSpan position)
    {
        if(!IsEnabled || position < _updateAt || _isUpdated || _animeModel.Tracking.WatchedEpisodes >= _currentEpisode)
        {
            return;
        }

        _isUpdated = true;
        var tracking = new Tracking() { WatchedEpisodes = _currentEpisode };

        if (_currentEpisode == _animeModel.TotalEpisodes)
        {
            tracking.Status = AnimeStatus.Completed;
            tracking.FinishDate = DateTime.Today;
        }
        else if (_currentEpisode == 1)
        {
            tracking.Status = AnimeStatus.Watching;
            tracking.StartDate = DateTime.Today;
        }

        _trackingService
            .Update(_animeModel.Id, tracking)
            .Subscribe(tracking => _animeModel.Tracking = tracking);
    }

    protected override void OnEpisodeChanged()
    {
        _isUpdated = false;
    }

    public bool IsEnabled => _animeModel is not null;
}

