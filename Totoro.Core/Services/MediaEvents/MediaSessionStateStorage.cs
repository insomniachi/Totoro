
namespace Totoro.Core.Services.MediaEvents;

internal class MediaSessionStateStorage(IResumePlaybackService playbackStateStorage) : MediaEventListener
{
    private readonly IResumePlaybackService _playbackStateStorage = playbackStateStorage;
    private bool _canUpdate;

    protected override void OnPositionChanged(TimeSpan position)
    {
        if (!IsEnabled)
        {
            return;
        }

        _playbackStateStorage.Update(_animeModel.Id, _currentEpisode, position.TotalSeconds);
    }

    protected override void OnPlaybackEnded()
    {
        if (!IsEnabled)
        {
            return;
        }

        _playbackStateStorage.Reset(_animeModel.Id, _currentEpisode);
    }

    protected override void OnEpisodeChanged()
    {
        _canUpdate = false;
    }

    protected override void OnPlay()
    {
        _canUpdate = true;
    }

    protected override void OnPaused()
    {
        _canUpdate = false;
    }

    public override void Stop()
    {
        _playbackStateStorage.SaveState();
    }

    private bool IsEnabled => _animeModel is not null && _currentEpisode > 0 && _canUpdate;

}

