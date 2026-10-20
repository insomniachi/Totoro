using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Services;

namespace Totoro.Agent;

public sealed record ScheduledRelease(
	[property: Description("The anime's id in the selected anime database.")]
	long Id,
	string Title,
	int Episode,
	[property: Description("When it airs, in the user's local time.")]
	DateTime AiringAt,
	[property: Description("Premiere (its season's first episode), Finale (its last) or None.")]
	ReleaseMarker Marker,
	[property: Description("Whether the user has watched it.")]
	bool Watched);

/// <summary>Agent tools for the release calendar (see <see cref="ReleaseScheduleService"/>).</summary>
public sealed class ReleaseScheduleTools(ReleaseScheduleService schedule, AnimeTrackingService tracking)
{
	[AgentTool("get_release_schedule")]
	[Description("When the episodes of the anime the user is watching air, in order: the ones the release calendar shows. E.g. what airs this week, or when a season's finale is.")]
	public async Task<IReadOnlyList<ScheduledRelease>> GetReleaseScheduleAsync(
		[Description("The first day, as yyyy-MM-dd; today if left out.")] DateTime? from = null,
		[Description("How many days from it.")] int days = 7)
	{
		AnimeTools.ThrowIfSignedOut(tracking);
		var start = (from ?? DateTime.Today).Date;
		List<Core.Modules.Anime.AnimeModel> watching;
		try
		{
			watching = await schedule.GetWatchingAsync();
		}
		catch (InvalidOperationException ex)
		{
			throw new AgentToolException(ex.Message);
		}

		return
		[
			.. ReleaseScheduleService.Between(watching, start, start.AddDays(Math.Clamp(days, 1, 366)))
									 .Select(x => new ScheduledRelease(x.Anime.Id, x.Anime.Title, x.Number, x.AiringAt, x.Marker, x.IsWatched)),
		];
	}
}
