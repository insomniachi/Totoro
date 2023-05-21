namespace Totoro.Plugins.Anime.Models;

public class AdditionalVideoStreamInformation
{
    public string? Title { get; init; }
    public List<Subtitle> Subtitles { get; } = new();
}
