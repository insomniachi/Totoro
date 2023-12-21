namespace Totoro.Plugins.Anime.Models;

public class VideoStream
{
    public string Resolution { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = [];
    public string Url { get; init; } = string.Empty;
}
