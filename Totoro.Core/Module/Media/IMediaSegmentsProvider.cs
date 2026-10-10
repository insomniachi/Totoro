namespace Totoro.Core.Module.Media;

public enum MediaSegmentType
{
	Recap,
	Opening,
	Ending,
}

public sealed record MediaSegment(MediaSegmentType Type, TimeSpan Start, TimeSpan End);

/// <summary>
/// Where an episode's opening, ending and recap are (e.g. Aniskip). Modules register implementations keyed by their
/// module id.
/// </summary>
public interface IMediaSegmentsProvider
{
	/// <param name="myAnimeListId">The anime's MyAnimeList id, which segment databases use.</param>
	/// <param name="duration">The episode's length; segments for other cuts of the episode (with a different length) are left out.</param>
	/// <returns>The segments, in order; none if they aren't known.</returns>
	Task<List<MediaSegment>> GetSegmentsAsync(long myAnimeListId, int episode, TimeSpan duration, CancellationToken ct);
}
