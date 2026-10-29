namespace Totoro.Core.Modules.Manga;

/// <summary>The user's manga list on a database (e.g. AniList). Modules register implementations keyed by their module id.</summary>
public interface IMangaTracker
{
	Guid Id { get; }
	string Name { get; }

	/// <summary>Saves the fields set in <paramref name="tracking"/>, adding the manga to the list if it isn't on it; returns what was saved.</summary>
	Task<MangaTracking> UpdateAsync(long id, MangaTracking tracking);

	/// <summary>False if it wasn't on the list.</summary>
	Task<bool> RemoveAsync(long id);

	Task<List<MangaModel>> GetUserListAsync(CancellationToken ct);
}
