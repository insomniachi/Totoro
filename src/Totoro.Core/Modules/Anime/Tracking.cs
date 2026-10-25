using System.ComponentModel;

namespace Totoro.Core.Modules.Anime;

public class Tracking
{
	public ListItemStatus? Status { get; set; }
	/// <summary>Out of 10; fractional when the tracker scores more finely (e.g. AniList out of 100).</summary>
	public double? Score { get; init; }
	public int? WatchedEpisodes { get; set; }
	public DateTime? StartDate { get; set; }
	public DateTime? FinishDate { get; set; }
	public DateTime? UpdatedAt { get; init; }

	/// <summary>The fields of the tracker's own <see cref="TrackingForm"/>, as edited; null leaves them as they are.</summary>
	public IReadOnlyList<TrackingField>? Fields { get; init; }

	/// <summary>The field with the given key if the user edited it; null to leave that value as it is.</summary>
	public T? Changed<T>(string key) where T : TrackingField => Fields?.OfType<T>().FirstOrDefault(x => x.Key == key && x.IsChanged);

	/// <summary>A copy without <see cref="Fields"/>, which belong to one tracker.</summary>
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
