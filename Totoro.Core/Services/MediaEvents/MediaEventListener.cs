using Totoro.Plugins.Anime.Contracts;

namespace Totoro.Core.Services.MediaEvents;

public abstract class MediaEventListener : IMediaEventListener
{
    protected AnimeModel _animeModel;
    protected IMediaPlayer _mediaPlayer;
    protected int _currentEpisode;
    protected ICatalogItem _searchResult;
    protected VideoStreamModel _videoStreamModel;
    protected TimestampResult _timeStamps;

    public void SetAnime(AnimeModel anime)
    {
        _animeModel = anime;
        OnAnimeChanged();
    }

    public void SetCurrentEpisode(int episode)
    {
        _currentEpisode = episode;
        OnEpisodeChanged();
    }

    public void SetTimeStamps(TimestampResult timeStamps)
    {
        _timeStamps = timeStamps;
        OnTimestampsChanged();
    }

    public void SetMediaPlayer(IMediaPlayer mediaPlayer)
    {
        _mediaPlayer ??= mediaPlayer;

        _mediaPlayer
            .DurationChanged
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(OnDurationChanged);

        _mediaPlayer
            .PositionChanged
            .Subscribe(OnPositionChanged);

        _mediaPlayer
            .PlaybackEnded
            .Subscribe(_ => OnPlaybackEnded());

        _mediaPlayer
            .Paused
            .Subscribe(_ => OnPaused());

        _mediaPlayer
            .Playing
            .Subscribe(_ => OnPlay());

        _mediaPlayer
            .TransportControls
            .OnDynamicSkip
            .Subscribe(_ => OnDynamicSkipped());

        _mediaPlayer
            .TransportControls
            .OnStaticSkip
            .Subscribe(_ => OnStaticSkipped());

        _mediaPlayer
            .TransportControls
            .OnNextTrack
            .Subscribe(_ => OnNextTrack());

        _mediaPlayer
            .TransportControls
            .OnPrevTrack
            .Subscribe(_ => OnPrevTrack());
    }

    public void SetSearchResult(ICatalogItem searchResult)
    {
        _searchResult = searchResult;
    }

    public void SetVideoStreamModel(VideoStreamModel videoStreamModel)
    {
        _videoStreamModel = videoStreamModel;
    }

    public virtual void Stop() { }
    protected virtual void OnPlay() { }
    protected virtual void OnPaused() { }
    protected virtual void OnPlaybackEnded() { }
    protected virtual void OnPositionChanged(TimeSpan position) { }
    protected virtual void OnDurationChanged(TimeSpan duration) { }
    protected virtual void OnEpisodeChanged() { }
    protected virtual void OnAnimeChanged() { }
    protected virtual void OnDynamicSkipped() { }
    protected virtual void OnStaticSkipped() { }
    protected virtual void OnTimestampsChanged() { }
    protected virtual void OnNextTrack() { }
    protected virtual void OnPrevTrack() { }
}

