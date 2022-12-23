using System.Diagnostics;
using System.Text.RegularExpressions;
using Humanizer;

namespace Totoro.Core.Models;

[DebuggerDisplay("{Anime} - {InfoText}")]
public sealed partial class AiredEpisode : IEquatable<AiredEpisode>
{
    public string Anime { get; set; }
    public string InfoText { get; set; }
    public string EpisodeUrl { get; set; }
    public string Image { get; set; }
    public DateTime TimeOfAiring { get; set; }
    public string HumanizedTimeOfAiring => TimeOfAiring.Humanize();
    public long? MalId { get; set; }
    public int GetEpisode()
    {
        var epMatch = EpisodeRegex().Match(EpisodeUrl);
        return epMatch.Success ? int.Parse(epMatch.Groups[1].Value) : 1;
    }

    public bool Equals(AiredEpisode other) => EpisodeUrl == other.EpisodeUrl;

    public override bool Equals(object obj)
    {
        return Equals(obj as AiredEpisode);
    }

    public override int GetHashCode() => EpisodeUrl.GetHashCode();

    [GeneratedRegex("ep(\\d+)")]
    private static partial Regex EpisodeRegex();
}
