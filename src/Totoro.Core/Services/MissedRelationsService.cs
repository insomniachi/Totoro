using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;

namespace Totoro.Core.Services;

/// <summary>
/// Sequels, prequels, side stories and spin-offs of what the user watched that aren't on their list, for Discover and
/// its See all page. Kept for the session (it takes a request per 50 anime per step down the season chains) until the list changes.
/// </summary>
public sealed class MissedRelationsService : IRecipient<AnimeTrackingChangedMessage>
{
	private readonly IFactory<IAnimeTracker> _trackers;
	private readonly IFactory<IAnimeMetadataProvider> _databases;
	private (Guid Tracker, Task<IReadOnlyList<MissedRelationGroup>> Missed)? _cached;

	public MissedRelationsService(IFactory<IAnimeTracker> trackers, IFactory<IAnimeMetadataProvider> databases)
	{
		_trackers = trackers;
		_databases = databases;
		WeakReferenceMessenger.Default.Register(this);
	}

	/// <summary>The missed anime, most popular first; empty with no database or list.</summary>
	public async Task<IReadOnlyList<AnimeModel>> GetAsync() =>
		[.. (await GetGroupsAsync()).SelectMany(x => x.Missed).OrderByDescending(x => x.Popularity)];

	/// <summary>
	/// The missed anime by franchise, most popular first; empty with no database or list. While they're still being
	/// found, <paramref name="progress"/> gets what's been found so far after each step down the season chains.
	/// </summary>
	public async Task<IReadOnlyList<MissedRelationGroup>> GetGroupsAsync(IProgress<IReadOnlyList<MissedRelationGroup>>? progress = null)
	{
		if (_trackers.CreateDefault() is not { } tracker || _databases.CreateDefault() is not { } database)
		{
			return [];
		}

		if (_cached is not { } cached || cached.Tracker != tracker.Id || cached.Missed.IsFaulted)
		{
			_found = [];
			_cached = cached = (tracker.Id, LoadAsync(tracker, database));
		}

		if (progress is null || cached.Missed.IsCompleted)
		{
			return await cached.Missed;
		}

		// shortcut: a load replaced mid-way (the list changed) still reports into this; the final result is right.
		Action<IReadOnlyList<MissedRelationGroup>> report = progress.Report;
		if (_found.Count > 0)
		{
			progress.Report(_found);
		}

		Found += report;
		try
		{
			return await cached.Missed;
		}
		finally
		{
			Found -= report;
		}
	}

	private IReadOnlyList<MissedRelationGroup> _found = [];

	private event Action<IReadOnlyList<MissedRelationGroup>>? Found;

	private async Task<IReadOnlyList<MissedRelationGroup>> LoadAsync(IAnimeTracker tracker, IAnimeMetadataProvider database)
	{
		var list = await tracker.GetUserListAsync(CancellationToken.None);
		var withRelations = await MissedRelations.WithRelationsAsync(list, ids => database.GetRelationsAsync(ids, CancellationToken.None), soFar =>
		{
			_found = MissedRelations.Group(list, soFar);
			Found?.Invoke(_found);
		});
		return withRelations.Count == 0 ? [] : MissedRelations.Group(list, withRelations);
	}

	/// <summary>Something added or removed may change what's missed: the next visit loads it again.</summary>
	public void Receive(AnimeTrackingChangedMessage message) => _cached = null;
}
