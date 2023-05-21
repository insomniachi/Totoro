namespace Totoro.Plugins.Anime.Models;

public class VideoStreamsForEpisode
{
    private int _episode;
    private string _episodeString = string.Empty;

    public int Episode
    {
        get => _episode;
        set
        {
            if(value <= 0)
            {
                return;
            }

            if(_episode == value)
            {
                return;
            }

            _episode = value;
            _episodeString = value.ToString();
        }
    }

    public string EpisodeString
    {
        get => _episodeString;
        set
        {
            if(string.IsNullOrEmpty(value))
            {
                return;
            }

            if(_episodeString == value)
            {
                return;
            }

            _episodeString = value;
            if(int.TryParse(value, out int epInt))
            {
                _episode = epInt;
            }
        }
    }

    public VideoStreams Streams { get; } = new();
    public AdditionalVideoStreamInformation AdditionalInformation { get; set; } = new();
    public List<Language> Audio { get; } = new() { Language.Japanese };
}
