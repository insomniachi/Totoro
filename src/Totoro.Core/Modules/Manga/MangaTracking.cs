using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Modules.Manga;

/// <summary>A manga's entry on the user's list; null fields are left as they are when it's saved.</summary>
public class MangaTracking
{
	/// <summary>The anime statuses: Watching means reading and Rewatching rereading (see <see cref="MediaText.Status"/>).</summary>
	public ListItemStatus? Status { get; set; }
	/// <summary>Out of 10.</summary>
	public double? Score { get; init; }
	public int? ChaptersRead { get; set; }
	public int? VolumesRead { get; set; }
	public DateTime? StartDate { get; set; }
	public DateTime? FinishDate { get; set; }
	public DateTime? UpdatedAt { get; init; }
}
