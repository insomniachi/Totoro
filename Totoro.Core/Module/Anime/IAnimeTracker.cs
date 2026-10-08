namespace Totoro.Core.Module.Anime;

public interface IAnimeTracker
{
	Guid Id { get; }
	public string Name { get; }
	Task<Tracking> Update(long id, Tracking tracking);
	Task<bool> Remove(long id);
	Task<List<AnimeModel>> GetUserList(CancellationToken ct);
}
