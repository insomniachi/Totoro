namespace Totoro.Core.Module.Anime;

public interface IAnimeTracker
{
	Guid Id { get; }
	public string Name { get; }
	Task<Tracking> UpdateAsync(long id, Tracking tracking);
	Task<bool> RemoveAsync(long id);
	Task<List<AnimeModel>> GetUserListAsync(CancellationToken ct);
}
