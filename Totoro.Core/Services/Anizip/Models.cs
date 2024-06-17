
using System.Text.Json.Serialization;

namespace Totoro.Core.Services.Anizip;

public class EpisodeInfo
{
    [JsonPropertyName("seasonNumber")]
    public int SeasonNumber { get; set; }

    [JsonPropertyName("episodeNumber")]
    public int EpisodeNumber { get; set; }

    [JsonPropertyName("absoluteEpisodeNumber")]
    public int AbsoluteEpisodeNumber { get; set; }

    [JsonPropertyName("title")]
    public Titles Titles { get; set; }

    [JsonPropertyName("overview")]
    public string Overview { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("airDate")]
    public string AirDate { get; set; }

    [JsonPropertyName("runtime")]
    public int Runtime { get; set; }

    [JsonPropertyName("airDateUtc")]
    public DateTime? AirDateUtc { get; set; }
}

public class Titles
{
    [JsonPropertyName("ja")]
    public string Japanese { get; set; }

    [JsonPropertyName("en")]
    public string English { get; set; }

    [JsonPropertyName("x-jat")]
    public string Romaji { get; set; }
}
