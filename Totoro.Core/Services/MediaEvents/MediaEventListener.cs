namespace Totoro.Core.Services.MediaEvents;

public abstract class MediaEventListener : IMediaEventListener
{
    protected IAnimeModel _animeModel;
    protected IMediaPlayer _mediaPlayer;
    protected int _currentEpisode;
    protected SearchResult _searchResult;
    protected VideoStreamModel _videoStreamModel;

    public void SetAnime(IAnimeModel anime)
    {
        _animeModel = anime;
        OnAnimeChanged();
    }

    public void SetCurrentEpisode(int episode)
    {
        _currentEpisode = episode;
        OnEpisodeChanged();
    }

    public void SetMediaPlayer(IMediaPlayer mediaPlayer)
    {
        _mediaPlayer ??= mediaPlayer;

        _mediaPlayer
            .DurationChanged
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
            .OnDynamicSkip
            .Subscribe(_ => OnDynamicSkipped());

        _mediaPlayer
            .OnStaticSkip
            .Subscribe(_ => OnStaticSkipped());
    }

    public void SetSearchResult(SearchResult searchResult)
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
}

