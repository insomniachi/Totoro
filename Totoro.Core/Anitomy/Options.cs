
namespace Anitomy;

public class Options(string delimiters = " _.&+,|", bool episode = true, bool title = true, bool extension = true, bool group = true)
{
    public string AllowedDelimiters { get; } = delimiters;
    public bool ParseEpisodeNumber { get; } = episode;
    public bool ParseEpisodeTitle { get; } = title;
    public bool ParseFileExtension { get; } = extension;
    public bool ParseReleaseGroup { get; } = group;
}
