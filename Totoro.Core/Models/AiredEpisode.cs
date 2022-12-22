using Humanizer;

namespace Totoro.Core.Models;

public sealed class AiredEpisode : IEquatable<AiredEpisode>
{
    public string Anime { get; set; }
    public string InfoText { get; set; }
    public string EpisodeUrl { get; set; }
    public string Image { get; set; }
    public DateTime TimeOfAiring { get; set; }
    public string HumanizedTimeOfAiring => TimeOfAiring.Humanize();
    public long Id { get; set; }

    public bool Equals(AiredEpisode other) => EpisodeUrl == other.EpisodeUrl;
}
