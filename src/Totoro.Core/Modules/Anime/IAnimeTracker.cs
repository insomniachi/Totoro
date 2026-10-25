namespace Totoro.Core.Modules.Anime;

public interface IAnimeTracker
{
	Guid Id { get; }
	public string Name { get; }
	Task<Tracking> UpdateAsync(long id, Tracking tracking);
	Task<bool> RemoveAsync(long id);
	Task<List<AnimeModel>> GetUserListAsync(CancellationToken ct);

	/// <summary>The score scale and the tracker's own fields, with the entry's current values, for the tracking dialog.</summary>
	Task<TrackingForm> GetFormAsync(long id, CancellationToken ct) => Task.FromResult(TrackingForm.Default);
}
