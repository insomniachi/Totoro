namespace Totoro.Plugins.Anime.Models;

public class AdditionalVideoStreamInformation
{
    public string? Title { get; set; }
    public List<Subtitle> Subtitles { get; } = [];
    public bool? IsMkv { get; set; }
}
