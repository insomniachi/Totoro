using System.ComponentModel;

namespace Totoro.Core.Models;

public enum AiringStatus
{
    [Description("Finished Airing")]
    FinishedAiring,
    [Description("Currently Airing")]
    CurrentlyAiring,
    [Description("Not Yet Aired")]
    NotYetAired
}