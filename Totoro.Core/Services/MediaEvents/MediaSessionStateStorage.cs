
namespace Totoro.Core.Services.MediaEvents;

internal class MediaSessionStateStorage : MediaEventListener
{
    private readonly IResumePlaybackService _playbackStateStorage;

    public MediaSessionStateStorage(IResumePlaybackService playbackStateStorage)
    {
        _playbackStateStorage = playbackStateStorage;
    }

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

    public override void Stop()
    {
        _playbackStateStorage.SaveState();
    }

    private bool IsEnabled => _animeModel is not null && _currentEpisode > 0;

}

