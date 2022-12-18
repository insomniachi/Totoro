using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Totoro.Core.Models;

public class AniSkipResult
{
    [JsonPropertyName("found")]
    public bool Success { get; set; }

    [JsonPropertyName("results")]
    public AniSkipResultItem[] Items { get; set; }
}

public class AniSkipResultItem
{
    [JsonPropertyName("interval")]
    public Interval Interval { get; set; }

    [JsonPropertyName("skipType")]
    public string SkipType { get; set; }

    [JsonPropertyName("skipId")]
    public string SkipId { get; set; }

    [JsonPropertyName("episodeLength")]
    public double EpisodeLength { get; set; }
}

[DebuggerDisplay("{StartTime} to {EndTime}")]
public class Interval
{
    [JsonPropertyName("startTime")]
    public double StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public double EndTime { get; set; }
}

[JsonSerializable(typeof(AniSkipResult))]
internal partial class AniSkipResultSerializerContext : JsonSerializerContext { }

