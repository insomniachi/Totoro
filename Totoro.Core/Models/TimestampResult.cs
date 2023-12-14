using System.Diagnostics;

namespace Totoro.Core.Models;

public class TimestampResult
{
    public bool Success { get; set; }
    public Timestamp[] Items { get; set; }
    public Timestamp Opening => Items.FirstOrDefault(x => x.SkipType == "Opening");
    public Timestamp Ending => Items.FirstOrDefault(x => x.SkipType == "Ending");
}

public class Timestamp
{
    public Interval Interval { get; set; }
    public string SkipType { get; set; }
    public Guid SkipId { get; set; }
    public double EpisodeLength { get; set; }
}

[DebuggerDisplay("{StartTime} to {EndTime}")]
public class Interval
{
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}

