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

	/// <summary>Whether it keeps the user's list on this PC while they aren't signed in, so they have one without an account.</summary>
	bool KeepsListWhenSignedOut => false;
}
