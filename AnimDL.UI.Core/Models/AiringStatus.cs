using System.ComponentModel;

namespace AnimDL.UI.Core.Models;

public enum AiringStatus
{
    [Description("Finished Airing")]
    FinishedAiring,
    [Description("Currently Airing")]
    CurrentlyAiring,
    [Description("Not Yet Aired")]
    NotYetAired
}