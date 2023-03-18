namespace Totoro.Core.Services.MediaEvents;

public interface IMediaEventListener
{
    void SetMediaPlayer(IMediaPlayer mediaPlayer);
    void SetAnime(IAnimeModel anime);
    void SetSearchResult(SearchResult searchResult);
    void SetCurrentEpisode(int episode);
    void Stop();
}


