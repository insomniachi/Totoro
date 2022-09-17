using System.ComponentModel;

namespace Totoro.Core.Models;

public enum AnimeStatus
{
    [Description("Watching")]
    Watching,
    [Description("Completed")]
    Completed,
    [Description("On-Hold")]
    OnHold,
    [Description("Plan to Watch")]
    PlanToWatch,
    [Description("Dropped")]
    Dropped,
    [Description("Selected status")]
    None
}