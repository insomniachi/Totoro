namespace Totoro.Core.Services.MediaEvents;

public abstract class MediaEventListener : IMediaEventListener
{
    protected IAnimeModel _animeModel;
    private IMediaPlayer _mediaPlayer;
    protected int _currentEpisode;
    protected SearchResult _searchResult;

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
            .Throttle(TimeSpan.FromSeconds(2))
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
    }

    public void SetSearchResult(SearchResult searchResult)
    {
        _searchResult = searchResult;
    }

    public virtual void Stop() { }

    protected virtual void OnPlay() { }
    protected virtual void OnPaused() { }
    protected virtual void OnPlaybackEnded() { }
    protected virtual void OnPositionChanged(TimeSpan position) { }
    protected virtual void OnDurationChanged(TimeSpan duration) { }
    protected virtual void OnEpisodeChanged() { }
    protected virtual void OnAnimeChanged() { }
}

