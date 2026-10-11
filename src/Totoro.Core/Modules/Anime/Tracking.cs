using System.ComponentModel;

namespace Totoro.Core.Modules.Anime;

public class Tracking
{
	public ListItemStatus? Status { get; set; }
	public int? Score { get; init; }
	public int? WatchedEpisodes { get; set; }
	public DateTime? StartDate { get; set; }
	public DateTime? FinishDate { get; set; }
	public DateTime? UpdatedAt { get; init; }

	public Tracking Clone()
	{
		return new Tracking
		{
			StartDate = StartDate,
			Score = Score,
			WatchedEpisodes = WatchedEpisodes,
			Status = Status,
			FinishDate = FinishDate,
			UpdatedAt = UpdatedAt
		};
	}
}

public enum ListItemStatus
{
	[Description("Watching")] Watching,

	[Description("Completed")] Completed,

	[Description("On Hold")] OnHold,

	[Description("Planning")] PlanToWatch,

	[Description("Dropped")] Dropped,

	[Description("Rewatching")] Rewatching,
}
