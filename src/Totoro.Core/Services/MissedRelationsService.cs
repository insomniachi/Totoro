using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;

namespace Totoro.Core.Services;

/// <summary>
/// Sequels, prequels, side stories and spin-offs of what the user watched that aren't on their list, for Discover and
/// its See all page. Kept for the session (it takes a request per 50 anime watched) until the list changes.
/// </summary>
public sealed class MissedRelationsService : IRecipient<AnimeTrackingChangedMessage>
{
	private readonly IFactory<IAnimeTracker, Guid> _trackers;
	private readonly IFactory<IAnimeMetadataProvider, Guid> _databases;
	private (Guid Tracker, Task<IReadOnlyList<AnimeModel>> Missed)? _cached;

	public MissedRelationsService(IFactory<IAnimeTracker, Guid> trackers, IFactory<IAnimeMetadataProvider, Guid> databases)
	{
		_trackers = trackers;
		_databases = databases;
		WeakReferenceMessenger.Default.Register(this);
	}

	/// <summary>The missed anime, most popular first; empty with no database or list.</summary>
	public Task<IReadOnlyList<AnimeModel>> GetAsync()
	{
		if (_trackers.CreateDefault() is not { } tracker || _databases.CreateDefault() is not { } database)
		{
			return Task.FromResult<IReadOnlyList<AnimeModel>>([]);
		}

		if (_cached is not { } cached || cached.Tracker != tracker.Id || cached.Missed.IsFaulted)
		{
			_cached = cached = (tracker.Id, LoadAsync(tracker, database));
		}

		return cached.Missed;
	}

	private static async Task<IReadOnlyList<AnimeModel>> LoadAsync(IAnimeTracker tracker, IAnimeMetadataProvider database)
	{
		var list = await tracker.GetUserListAsync(CancellationToken.None);
		var watched = list.Where(MissedRelations.IsWatched).Select(x => x.Id).ToList();
		return watched.Count == 0 ? [] : MissedRelations.Find(list, await database.GetRelationsAsync(watched, CancellationToken.None));
	}

	/// <summary>Something added or removed may change what's missed: the next visit loads it again.</summary>
	public void Receive(AnimeTrackingChangedMessage message) => _cached = null;
}
